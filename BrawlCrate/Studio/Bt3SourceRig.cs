using BrawlCrate.Studio.Retargeting;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;

namespace BrawlCrate.Studio
{
    // Read-only source evaluator for the exact native pmdl/BPE pair. A solver
    // may consume this rig only after reviewed bindings and motion validation;
    // this type does not create a CHR0 or replace the game source resources.
    internal sealed class Bt3SourceRig
    {
        internal sealed class SkinnedVertex
        {
            internal int Owner,Parent;
            internal float OwnerWeight;
            internal Vector3 OwnerLocal,ParentLocal;
            internal Vector3 Evaluate(RigPose pose)
            {
                Vector3 a=Vector3.Transform(OwnerLocal,pose.World[Owner]);
                Vector3 b=Parent<0?ParentLocal:Vector3.Transform(ParentLocal,pose.World[Parent]);
                return Vector3.Lerp(b,a,OwnerWeight);
            }
        }

        internal Rig Rig {get;}
        internal Bt3NativeMesh.Scene Scene {get;}
        internal Bt3NativeAssetReader.Motion Motion {get;}
        internal SkinnedVertex[] Vertices {get;}
        internal SkeletonCharacterization RoleProposal {get;}
        internal float MaximumBindSplit {get;}
        private readonly Dictionary<int,int> indexById;

        internal Bt3SourceRig(Bt3NativeMesh.Scene scene,Bt3NativeAssetReader.Motion motion)
        {
            Scene=scene??throw new ArgumentNullException(nameof(scene));
            Motion=motion??throw new ArgumentNullException(nameof(motion));
            var bones=scene.Skeleton.Bones;
            indexById=bones.Select((bone,index)=>new{bone.Id,Index=index}).ToDictionary(p=>p.Id,p=>p.Index);
            if(motion.Tracks.Any(t=>!indexById.ContainsKey(t.Bone)))
                throw new InvalidDataException("BT3 action references a joint outside its exact costume rig.");
            var joints=bones.Select(b=>new RigJoint("BT3_J"+b.Id.ToString("D2"),
                b.Parent<0?-1:indexById[b.Parent],
                new JointTransform(-b.Rest,Quaternion.Identity,Vector3.One))).ToArray();
            RoleProposal=SkeletonCharacterizer.Analyze(joints);
            var preliminary=new Rig("BT3 pmdl "+scene.Skeleton.SourceSha256,"BT3 native",joints,RoleProposal.Roles,1,
                coordinateUp:Vector3.UnitY);
            var vertices=new List<SkinnedVertex>();float split=0,top=float.MinValue,bottom=float.MaxValue;
            foreach(var batch in scene.Batches)
                foreach(var v in batch.Triangles)
                {
                    int id=v.Bone<0?batch.Bone:v.Bone;
                    if(!indexById.TryGetValue(id,out int owner))throw new InvalidDataException("BT3 skin owner is outside its skeleton.");
                    int nativeParent=bones[owner].Parent;
                    int parent=nativeParent<0?-1:indexById[nativeParent];
                    var pivotA=v.Bone<0?batch.PivotA:scene.Pivots[id].A;
                    var pivotB=v.Bone<0?batch.PivotB:scene.Pivots[id].B;
                    var entry=new SkinnedVertex{Owner=owner,Parent=parent,OwnerWeight=v.Weight,
                        OwnerLocal=-(v.Position-pivotA),ParentLocal=-(v.Position-pivotB)};
                    var a=Vector3.Transform(entry.OwnerLocal,preliminary.Rest.World[owner]);
                    var b=parent<0?entry.ParentLocal:Vector3.Transform(entry.ParentLocal,preliminary.Rest.World[parent]);
                    split=Math.Max(split,Vector3.Distance(a,b));vertices.Add(entry);
                    float height=Vector3.Lerp(b,a,entry.OwnerWeight).Y;
                    top=Math.Max(top,height);bottom=Math.Min(bottom,height);
                }
            if(vertices.Count==0||vertices.Count>2000000||split>0.0001f)
                throw new InvalidDataException("BT3 native two-influence skin has an unverified bind or work bound.");
            float headRadius=top-bottom;
            if(RoleProposal.Roles.TryGetValue("head",out int head))
            {
                headRadius=top-preliminary.Rest.Point(head).Y;
                if(headRadius<.1f&&RoleProposal.Roles.TryGetValue("neck",out int neck))
                    headRadius=Vector3.Distance(preliminary.Rest.Point(head),preliminary.Rest.Point(neck))*2;
            }
            if(!RigMath.Finite(headRadius)||headRadius<=0)
                throw new InvalidDataException("BT3 source mesh has no finite positive head/height scale reference.");
            Rig=new Rig("BT3 pmdl "+scene.Skeleton.SourceSha256,"BT3 native",joints,RoleProposal.Roles,headRadius,
                coordinateUp:Vector3.UnitY);
            Vertices=vertices.ToArray();MaximumBindSplit=split;
            // Contact evidence uses the original two-pivot skin points. It
            // does not replace a native bind, normalize weights or infer a
            // floor from a joint origin.
            foreach(string side in new[]{"left","right"})
            foreach(string end in new[]{"foot","hand"})
            {
                string role=side+"."+end;
                if(!Rig.Roles.TryGetValue(role,out int joint))continue;
                var points=new List<SkinPoint>();
                foreach(var vertex in Vertices)
                {
                    if(vertex.Parent<0)continue;
                    float membership=(Rig.Ancestors[vertex.Owner][joint]?vertex.OwnerWeight:0)+
                        (Rig.Ancestors[vertex.Parent][joint]?1-vertex.OwnerWeight:0);
                    if(membership<.8f)continue;
                    points.Add(new SkinPoint{Weights=new[]{
                        new SkinWeight{Joint=vertex.Owner,Local=vertex.OwnerLocal,Weight=vertex.OwnerWeight},
                        new SkinWeight{Joint=vertex.Parent,Local=vertex.ParentLocal,Weight=1-vertex.OwnerWeight}}});
                }
                if(points.Count<4)continue;
                Vector3 forward=end=="foot"&&Rig.Roles.ContainsKey(side+".toe")?
                    Rig.Rest.Point(side+".toe")-Rig.Rest.Point(role):
                    end=="hand"&&Rig.Roles.ContainsKey(side+".finger1.a")?
                    Rig.Rest.Point(side+".finger1.a")-Rig.Rest.Point(role):
                    Vector3.Transform(Vector3.UnitZ,RigMath.BodyFrame(Rig));
                Vector3 normal=end=="hand"&&Rig.Roles.ContainsKey(side+".finger0.a")&&
                    Rig.Roles.ContainsKey(side+".finger3.a")?
                    Vector3.Cross(forward,Rig.Rest.Point(side+".finger0.a")-Rig.Rest.Point(side+".finger3.a")):
                    Vector3.UnitY;
                if(forward.LengthSquared()<1e-10f)forward=Vector3.UnitZ;
                if(normal.LengthSquared()<1e-10f)normal=Vector3.UnitY;
                Quaternion inverse=Quaternion.Inverse(Rig.Rest.Orientation[joint]);
                Rig.Surfaces.Add(role,new ContactSurface{Role=role,Points=points.ToArray(),
                    NormalLocal=Vector3.Transform(RigMath.Unit(normal),inverse),
                    ForwardLocal=Vector3.Transform(RigMath.Unit(forward),inverse)});
            }
            if(new[]{"left.foot","right.foot"}.All(Rig.Surfaces.ContainsKey))
                Rig.GroundHeight=new[]{"left.foot","right.foot"}
                    .Min(role=>Rig.Surfaces[role].Sample(Rig.Rest).Clearance);
            CaptureBodySurfaces();
        }

        private void CaptureBodySurfaces()
        {
            var patches=new Dictionary<string,string[]> {
                ["pelvis"]=new[]{"pelvis","spine","chest","neck"},
                ["head"]=new[]{"head"}};
            foreach(string side in new[]{"left","right"})
            {
                patches[side+".lowerArm"]=new[]{side+".shoulder",side+".upperArm",side+".lowerArm"};
                patches[side+".lowerLeg"]=new[]{side+".upperLeg",side+".lowerLeg"};
            }
            var roles=patches.Values.SelectMany(v=>v).Distinct().Where(Rig.Roles.ContainsKey).ToArray();
            var nearest=new string[Rig.Joints.Length];
            for(int i=0;i<nearest.Length;i++)
                for(int joint=i;joint>=0;joint=Rig.Joints[joint].Parent)
                {
                    string role=roles.FirstOrDefault(r=>Rig.Roles[r]==joint);
                    if(role==null)continue;
                    nearest[i]=role;break;
                }
            var grouped=roles.ToDictionary(r=>r,r=>new List<SkinPoint>());
            bool[] accessory=Enumerable.Range(0,Rig.Joints.Length)
                .Select(i=>BrawlRetargeter.IsAccessoryBranch(Rig,i)).ToArray();
            foreach(var vertex in Vertices)
            {
                if(vertex.Parent<0)continue;
                if((vertex.OwnerWeight>.01f&&accessory[vertex.Owner])||
                    (1-vertex.OwnerWeight>.01f&&accessory[vertex.Parent]))continue;
                string owner=nearest[vertex.Owner],parent=nearest[vertex.Parent];
                string role=vertex.OwnerWeight>=.5f?owner:parent;
                float weight=owner==parent?1:vertex.OwnerWeight>=.5f?vertex.OwnerWeight:1-vertex.OwnerWeight;
                if(role==null||weight<.35f)continue;
                grouped[role].Add(new SkinPoint{Weights=new[]{
                    new SkinWeight{Joint=vertex.Owner,Local=vertex.OwnerLocal,Weight=vertex.OwnerWeight},
                    new SkinWeight{Joint=vertex.Parent,Local=vertex.ParentLocal,Weight=1-vertex.OwnerWeight}}});
            }
            foreach(var patch in patches)
            {
                var points=patch.Value.Where(grouped.ContainsKey).SelectMany(r=>grouped[r]).ToArray();
                if(points.Length>=4 && !Rig.Surfaces.ContainsKey(patch.Key))
                    Rig.Surfaces.Add(patch.Key,new ContactSurface{Role=patch.Key,Points=points});
            }
        }

        internal RigPose Sample(float frame)
        {
            var native=Bt3NativeAssetReader.Sample(Scene.Skeleton,Motion,frame);
            var locals=new JointTransform[native.Translation.Length];
            for(int i=0;i<locals.Length;i++)
                locals[i]=new JointTransform(-native.Translation[i],native.Rotation[i],Vector3.One);
            return new RigPose(Rig,locals);
        }

        // Independent reference from the same decoded native pose, using the
        // game's owner/parent two-pivot equation rather than SkinPoint.Evaluate.
        // This comparison tests the coordinate bridge and all original weights.
        internal float MaximumPositionDifference(float frame)
        {
            var native=Bt3NativeAssetReader.Sample(Scene.Skeleton,Motion,frame);
            var world=new Matrix4x4[native.Translation.Length];
            for(int i=0;i<world.Length;i++)
            {
                var bone=Scene.Skeleton.Bones[i];
                var local=Matrix4x4.CreateFromQuaternion(native.Rotation[i])*
                    Matrix4x4.CreateTranslation(native.Translation[i]);
                world[i]=bone.Parent<0?local:local*world[indexById[bone.Parent]];
            }
            var projected=Sample(frame);float maximum=0;int vertex=0;
            foreach(var batch in Scene.Batches)foreach(var v in batch.Triangles)
            {
                int id=v.Bone<0?batch.Bone:v.Bone,owner=indexById[id],parent=Scene.Skeleton.Bones[owner].Parent;
                var pivotA=v.Bone<0?batch.PivotA:Scene.Pivots[id].A;
                var pivotB=v.Bone<0?batch.PivotB:Scene.Pivots[id].B;
                var a=Vector3.Transform(v.Position-pivotA,world[owner]);
                var b=parent<0?v.Position-pivotB:Vector3.Transform(v.Position-pivotB,world[indexById[parent]]);
                var expected=-Vector3.Lerp(b,a,v.Weight);
                maximum=Math.Max(maximum,Vector3.Distance(expected,Vertices[vertex++].Evaluate(projected)));
            }
            return maximum;
        }
    }
}
