using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;

namespace BrawlCrate.Studio.Retargeting
{
    public enum GroundContactMode { Auto, Grounded, Airborne, SourceContacts }
    // Format-neutral, parent-before-child SRT data. Native readers/writers stay in adapters.
    public struct JointTransform
    {
        public Vector3 Translation, Scale;
        public Quaternion Rotation;
        public JointTransform(Vector3 translation, Quaternion rotation, Vector3 scale)
        { Translation = translation; Rotation = Quaternion.Normalize(rotation); Scale = scale; }
    }

    public sealed class RigJoint
    {
        public string Name { get; }
        public int Parent { get; }
        public JointTransform Bind { get; }
        public RigJoint(string name, int parent, JointTransform bind) { Name = name; Parent = parent; Bind = bind; }
    }

    public sealed class Rig
    {
        public string Name { get; }
        public string Profile { get; }
        public RigJoint[] Joints { get; }
        public Dictionary<string, int> Roles { get; }
        public RigPose Rest { get; }
        internal int[][] Branches { get; }
        internal bool[][] Ancestors { get; }
        public float HeadRadius { get; }
        // Immutable adapter/review evidence for coordinate-up. It is separate
        // from the mutable contact/support plane and never changes native binds.
        // Null retains the labelled legacy anatomical-rest fallback.
        public Vector3? CoordinateUp { get; }
        // A complete exact-rig user/adapter review takes precedence over the
        // anatomical heading estimate. Quaternion frames are proper rotations;
        // reflected/left-handed coordinate systems need a separate adapter.
        public Quaternion? CoordinateFrame { get; }
        public Dictionary<string, ContactSurface> Surfaces { get; } = new Dictionary<string, ContactSurface>();
        public Vector3 GroundNormal { get; set; } = Vector3.UnitY;
        public float GroundHeight { get; set; }
        public Rig(string name, string profile, IEnumerable<RigJoint> joints, IDictionary<string, int> roles, float headRadius)
            : this(name, profile, joints, roles, headRadius, null, null) { }
        public Rig(string name, string profile, IEnumerable<RigJoint> joints, IDictionary<string, int> roles, float headRadius, Vector3? coordinateUp = null, Quaternion? coordinateFrame = null)
        {
            Name = name; Profile = profile; Joints = joints.ToArray(); Roles = new Dictionary<string, int>(roles);
            if (coordinateFrame.HasValue)
            {
                Quaternion frame = coordinateFrame.Value;
                if (!RigMath.Finite(frame.X) || !RigMath.Finite(frame.Y) || !RigMath.Finite(frame.Z) || !RigMath.Finite(frame.W) ||
                    !RigMath.Finite(frame.LengthSquared()) || frame.LengthSquared() < 1e-10f)
                    throw new InvalidDataException("The reviewed coordinate frame must be a finite nonzero proper rotation.");
                CoordinateFrame = Quaternion.Normalize(frame);
            }
            if (coordinateUp.HasValue)
            {
                Vector3 up = coordinateUp.Value;
                if (!RigMath.Finite(up) || up.LengthSquared() < 1e-10f || !RigMath.Finite(up.LengthSquared()))
                    throw new InvalidDataException("The declared coordinate-up must be a finite nonzero axis.");
                CoordinateUp = Vector3.Normalize(up);
            }
            if (CoordinateFrame.HasValue)
            {
                Vector3 reviewedUp = Vector3.Transform(Vector3.UnitY, CoordinateFrame.Value);
                if (CoordinateUp.HasValue && Vector3.Dot(reviewedUp, CoordinateUp.Value) < .9999f)
                    throw new InvalidDataException("The declared up axis disagrees with the reviewed coordinate frame.");
                CoordinateUp = reviewedUp;
            }
            if (Joints.Length == 0 || Joints.Length > 512 || Joints.Select(j => j.Name).Distinct(StringComparer.Ordinal).Count() != Joints.Length)
                throw new InvalidDataException("A retarget rig needs unique joints and at most 512 bones.");
            for (int i = 0; i < Joints.Length; i++)
                if (Joints[i].Parent >= i || Joints[i].Parent < -1 || !RigMath.Finite(Joints[i].Bind.Translation) ||
                    !RigMath.Finite(Joints[i].Bind.Rotation.X) || !RigMath.Finite(Joints[i].Bind.Rotation.Y) ||
                    !RigMath.Finite(Joints[i].Bind.Rotation.Z) || !RigMath.Finite(Joints[i].Bind.Rotation.W) ||
                    !RigMath.Finite(Joints[i].Bind.Scale) || Joints[i].Bind.Scale.X <= 0 || Joints[i].Bind.Scale.Y <= 0 || Joints[i].Bind.Scale.Z <= 0)
                    throw new InvalidDataException("The retarget rig has an invalid hierarchy or nonpositive bind scale.");
            if (Roles.Values.Any(i => i < 0 || i >= Joints.Length) || Roles.Values.Distinct().Count() != Roles.Count)
                throw new InvalidDataException("Humanoid roles must identify distinct existing joints.");
            // A world-space edit only changes this joint and its descendants.
            // Cache the topologically ordered branches once, rather than
            // recomputing every unrelated joint for each IK control update.
            Ancestors=Enumerable.Range(0,Joints.Length).Select(i=>new bool[Joints.Length]).ToArray();
            for(int i=0;i<Joints.Length;i++)
            {
                if(Joints[i].Parent>=0)Array.Copy(Ancestors[Joints[i].Parent],Ancestors[i],Joints.Length);
                Ancestors[i][i]=true;
            }
            Branches = Enumerable.Range(0,Joints.Length).Select(root => Enumerable.Range(root,Joints.Length-root).Where(i => Ancestors[i][root]).ToArray()).ToArray();
            HeadRadius = Math.Max(0.01f, headRadius);
            Rest = new RigPose(this);
        }
    }

    public sealed class RigPose
    {
        public Rig Rig { get; }
        public JointTransform[] Local { get; }
        public Matrix4x4[] World { get; }
        public Quaternion[] Orientation { get; }
        private int revision,planeRevision=-1;
        private Vector3 planeNormal;
        private readonly double[] groundPlanes;
        public RigPose(Rig rig, JointTransform[] local = null)
        {
            Rig = rig; Local = local == null ? rig.Joints.Select(j => j.Bind).ToArray() : (JointTransform[])local.Clone();
            if (Local.Length != rig.Joints.Length) throw new ArgumentException("Pose and rig joint counts differ.");
            World = new Matrix4x4[Local.Length]; Orientation = new Quaternion[Local.Length];
            groundPlanes=new double[Local.Length*4];Update();
        }
        public Vector3 Point(int i) => new Vector3(World[i].M41, World[i].M42, World[i].M43);
        internal RigPose(RigPose reference,JointTransform[] local,int[] changedBones)
        {
            Rig=reference.Rig;Local=(JointTransform[])local.Clone();
            World=(Matrix4x4[])reference.World.Clone();Orientation=(Quaternion[])reference.Orientation.Clone();
            groundPlanes=new double[Local.Length*4];
            var changed=new bool[Local.Length];foreach(int joint in changedBones)changed[joint]=true;
            for(int joint=0;joint<Local.Length;joint++)
            {
                int parent=Rig.Joints[joint].Parent;
                if(parent>=0 && changed[parent])changed[joint]=true;
                if(changed[joint])UpdateJoint(joint);
            }
        }
        public Vector3 Point(string role) => Point(Rig.Roles[role]);
        public void Update()
        {
            for (int i = 0; i < Local.Length; i++) UpdateJoint(i);
        }
        internal void UpdateJoint(int i)
        {
                JointTransform value = Local[i]; int parent = Rig.Joints[i].Parent;
                Matrix4x4 local = Matrix4x4.CreateScale(value.Scale) * Matrix4x4.CreateFromQuaternion(value.Rotation) * Matrix4x4.CreateTranslation(value.Translation);
                World[i] = parent < 0 ? local : local * World[parent];
                Orientation[i] = parent < 0 ? value.Rotation : Quaternion.Normalize(Orientation[parent] * value.Rotation);
                if (!RigMath.Finite(Point(i))) throw new InvalidDataException("Retargeting produced a non-finite joint pose.");
                unchecked{revision++;}
        }
        internal double[] GroundPlaneCoefficients()
        {
            Vector3 n=Rig.GroundNormal;
            if(planeRevision==revision && planeNormal==n)return groundPlanes;
            for(int i=0;i<World.Length;i++)
            {
                Matrix4x4 m=World[i];int index=i*4;
                groundPlanes[index]=(double)m.M11*n.X+(double)m.M12*n.Y+(double)m.M13*n.Z;
                groundPlanes[index+1]=(double)m.M21*n.X+(double)m.M22*n.Y+(double)m.M23*n.Z;
                groundPlanes[index+2]=(double)m.M31*n.X+(double)m.M32*n.Y+(double)m.M33*n.Z;
                groundPlanes[index+3]=(double)m.M41*n.X+(double)m.M42*n.Y+(double)m.M43*n.Z;
            }
            planeRevision=revision;planeNormal=n;return groundPlanes;
        }
        public void SetWorldRotation(int joint, Quaternion rotation)
        {
            int parent = Rig.Joints[joint].Parent;
            Local[joint].Rotation = Quaternion.Normalize(parent < 0 ? rotation : Quaternion.Inverse(Orientation[parent]) * rotation);
            foreach(int i in Rig.Branches[joint])UpdateJoint(i);
        }
        public void SetWorldPoint(int joint, Vector3 point)
        {
            int parent = Rig.Joints[joint].Parent;
            if (parent < 0) Local[joint].Translation = point;
            else
            {
                if (!Matrix4x4.Invert(World[parent], out Matrix4x4 inverse)) throw new InvalidDataException("A pose has a singular parent transform.");
                Local[joint].Translation = Vector3.Transform(point, inverse);
            }
            foreach(int i in Rig.Branches[joint])UpdateJoint(i);
        }
    }

    public static class RigMath
    {
        public static bool Finite(Vector3 v) => Finite(v.X) && Finite(v.Y) && Finite(v.Z);
        public static bool Finite(float v) => !float.IsNaN(v) && !float.IsInfinity(v);
        public static float Clamp(float v, float min, float max) => Math.Max(min, Math.Min(max, v));
        public static Vector3 Unit(Vector3 v)
        { if (v.LengthSquared() < 1e-10f) throw new InvalidDataException("A required rig direction has zero length."); return Vector3.Normalize(v); }
        public static Quaternion FromTo(Vector3 from, Vector3 to)
        {
            from = Unit(from); to = Unit(to); float dot = Clamp(Vector3.Dot(from, to), -1, 1);
            if (dot > 0.999999f) return Quaternion.Identity;
            if (dot < -0.999999f)
            {
                Vector3 axis = Vector3.Cross(from, Math.Abs(from.X) < 0.8f ? Vector3.UnitX : Vector3.UnitY);
                return Quaternion.CreateFromAxisAngle(Unit(axis), (float)Math.PI);
            }
            return Quaternion.Normalize(new Quaternion(Vector3.Cross(from, to), 1 + dot));
        }
        public static float RotationError(Quaternion a, Quaternion b) =>
            2 * (float)Math.Acos(Clamp(Math.Abs(Quaternion.Dot(Quaternion.Normalize(a), Quaternion.Normalize(b))), 0, 1));
        public static Quaternion BodyFrame(Rig rig)
        {
            if (rig.CoordinateFrame.HasValue) return rig.CoordinateFrame.Value;
            Vector3 right = Unit(rig.Rest.Point("left.upperArm") - rig.Rest.Point("right.upperArm"));
            Vector3 up;
            if (rig.CoordinateUp.HasValue)
            {
                // Head/neck position is authored anatomy/posture, not a game
                // coordinate axis. Keep the reviewed lateral heading in the
                // plane perpendicular to the adapter's declared up direction.
                up = rig.CoordinateUp.Value;
                right -= up * Vector3.Dot(right, up);
                if (right.LengthSquared() < 1e-10f)
                    throw new InvalidDataException("The declared coordinate-up is parallel to the reviewed lateral axis; review coordinate evidence.");
                right = Unit(right);
            }
            else
            {
                up = rig.Rest.Point("head") - rig.Rest.Point("pelvis");
                up = Unit(up - right * Vector3.Dot(up, right));
            }
            Vector3 forward = Unit(Vector3.Cross(right, up));
            var matrix = new Matrix4x4(right.X, right.Y, right.Z, 0, up.X, up.Y, up.Z, 0,
                forward.X, forward.Y, forward.Z, 0, 0, 0, 0, 1);
            return Quaternion.Normalize(Quaternion.CreateFromRotationMatrix(matrix));
        }
        public static Quaternion ReviewedCoordinateFrame(Vector3 right, Vector3 up, Vector3 forward)
        {
            var axes = new[] {right, up, forward};
            if (axes.Any(v => !Finite(v) || Math.Abs(v.LengthSquared() - 1) > .0001f) ||
                Math.Abs(Vector3.Dot(right, up)) > .0001f || Math.Abs(Vector3.Dot(right, forward)) > .0001f || Math.Abs(Vector3.Dot(up, forward)) > .0001f)
                throw new InvalidDataException("Reviewed coordinates require finite orthogonal unit right, up and forward axes.");
            if (Vector3.Dot(Vector3.Cross(right, up), forward) < .9999f)
                throw new NotSupportedException("The reviewed coordinate axes are left-handed. Original Next requires a reviewed proper frame; reflected-axis conversion needs a separate adapter.");
            var matrix = new Matrix4x4(right.X, right.Y, right.Z, 0, up.X, up.Y, up.Z, 0, forward.X, forward.Y, forward.Z, 0, 0, 0, 0, 1);
            return Quaternion.Normalize(Quaternion.CreateFromRotationMatrix(matrix));
        }
    }
}
