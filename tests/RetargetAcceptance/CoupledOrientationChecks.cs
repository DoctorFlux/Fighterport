using BrawlCrate.Studio.Retargeting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Reflection;

internal static partial class Program
{
    static void CoupledOrientationChecks()
    {
        var solve=typeof(ContactMotion).GetMethod("FitBodyContacts",BindingFlags.Instance|BindingFlags.NonPublic);
        var lever=typeof(ContactMotion).GetMethod("ContactLever",BindingFlags.Static|BindingFlags.NonPublic);
        var global=Quaternion.CreateFromAxisAngle(Vector3.Normalize(new Vector3(1,2,-3)),.73f);
        foreach(bool permuted in new[]{false,true})foreach(float size in new[]{.4f,1.8f})foreach(var frame in new[]{Quaternion.Identity,global})
        {
            var source=CoordinateFixture("source-opaque-",1,.3f,2,frame,true,!permuted);
            var target=CoordinateFixture("target-opaque-",size,-.7f,7,frame,true,permuted);
            foreach(var fixture in new[]{source,target})
            {
                var rig=fixture.Rig;rig.GroundNormal=Vector3.Transform(Vector3.UnitY,frame);rig.GroundHeight=0;
                foreach(string side in new[]{"left","right"})
                {
                    string role=side+".foot";int end=rig.Roles[role];
                    int[] influences={rig.Roles["pelvis"],rig.Roles[side+".upperLeg"],rig.Roles[side+".lowerLeg"],end,rig.Roles[side+".toe"]};
                    Vector3 pivot=rig.Rest.Point(end)-rig.GroundNormal*Vector3.Dot(rig.Rest.Point(end),rig.GroundNormal);
                    var points=new List<SkinPoint>();
                    foreach(float x in new[]{-.2f,.2f})foreach(float z in new[]{-.3f,.3f})
                    {
                        float unit=Vector3.Distance(rig.Rest.Point("left.upperLeg"),rig.Rest.Point("left.foot"))/8;
                        var point=pivot+Vector3.Transform(new Vector3(x,0,z)*unit,frame);
                        points.Add(new SkinPoint{Weights=influences.Select((bone,index)=>{
                            Matrix4x4.Invert(rig.Rest.World[bone],out var inverse);
                            return new SkinWeight{Joint=bone,Local=Vector3.Transform(point,inverse),Weight=new[]{.025f,.025f,.05f,.7f,.2f}[index]};
                        }).ToArray()});
                    }
                    rig.Surfaces[role]=new ContactSurface{Role=role,Points=points.ToArray(),
                        NormalLocal=Vector3.Transform(rig.GroundNormal,Quaternion.Inverse(rig.Rest.Orientation[end])),ForwardLocal=Vector3.UnitZ};
                }
            }
            var plan=new HumanoidRetargeter(source.Rig,target.Rig);
            var contacts=new ContactMotion(plan,t=>new RigPose(source.Rig,source.Rig.Joints.Select(j=>j.Bind).ToArray()),1,true,requireFloorEvidence:false);
            var local=target.Rig.Joints.Select(j=>j.Bind).ToArray();
            local[0].Translation+=Vector3.Transform(new Vector3(.37f,0,-.29f)*size,frame);
            local[0].Rotation=Quaternion.Normalize(Quaternion.CreateFromAxisAngle(target.Rig.GroundNormal,.41f)*local[0].Rotation);
            var pose=new RigPose(target.Rig,local);
            pose.SetWorldPoint(target.Rig.Roles["pelvis"],pose.Point("pelvis")-target.Rig.GroundNormal*.03f*size);
            var before=(JointTransform[])pose.Local.Clone();
            var required=new[]{"left.foot","right.foot"}.ToDictionary(role=>target.Rig.Roles[role],role=>pose.Orientation[target.Rig.Roles[role]]);
            var controls=new[]{"left.foot","right.foot"}.ToDictionary(role=>role,role=>new ContactGoal{Support=1,Lock=0});
            solve.Invoke(contacts,new object[]{pose,controls,null,true});
            double orientation=required.Max(p=>SpanAngle(pose.Orientation[p.Key],p.Value));
            float penetration=target.Rig.Surfaces.Values.Max(s=>Math.Max(0,-s.Clearance(pose)));
            float limb=Vector3.Distance(target.Rig.Rest.Point("left.upperLeg"),target.Rig.Rest.Point("left.lowerLeg"))+Vector3.Distance(target.Rig.Rest.Point("left.lowerLeg"),target.Rig.Rest.Point("left.foot"));
            Check(contacts.BodySolveIterations>0&&orientation<.001,"Coupled full-patch floor solve retains both intended end orientations on opaque unequal and permuted rigs",new{size,permuted,orientation,contacts.BodySolveIterations});
            Check(penetration<=limb*.001,"Orientation-constrained solve retains the unchanged full-skin contact position bound",new{penetration,limb});
            Check(pose.Local[0].Equals(before[0])&&pose.Local.Select(p=>p.Scale).SequenceEqual(before.Select(p=>p.Scale)),"Coupled orientation constraints preserve original motion control and every scale");
            var positions=pose.World.Select(m=>m.Translation).ToArray();
            var patch=target.Rig.Surfaces["left.foot"].Points[0];double maximum=0;
            foreach(int joint in new[]{target.Rig.Roles["pelvis"],target.Rig.Roles["left.upperLeg"],target.Rig.Roles["left.lowerLeg"],target.Rig.Roles["left.toe"]})
            foreach(var axis in new[]{Vector3.UnitX,Vector3.UnitY,Vector3.UnitZ})
            {
                Vector3 expected=Vector3.Zero;
                foreach(var weight in patch.Weights.Where(w=>target.Rig.Ancestors[w.Joint][joint]))
                {
                    var point=Vector3.Transform(weight.Local,pose.World[weight.Joint]);
                    var offset=(Vector3)lever.Invoke(null,new object[]{target.Rig,positions,weight.Joint,point,joint,required});
                    expected+=Vector3.Cross(axis,offset)*weight.Weight;
                }
                Vector3 Sample(float delta)
                {
                    var copy=new RigPose(target.Rig,(JointTransform[])pose.Local.Clone());
                    copy.SetWorldRotation(joint,Quaternion.CreateFromAxisAngle(axis,delta)*copy.Orientation[joint]);
                    foreach(var end in required.OrderBy(p=>p.Key))copy.SetWorldRotation(end.Key,end.Value);
                    return patch.Evaluate(copy);
                }
                maximum=Math.Max(maximum,Vector3.Distance(expected,(Sample(.001f)-Sample(-.001f))/.002f)/size);
            }
            Check(maximum<.01,"Independent central FK differences verify the constrained full-five-influence positional Jacobian",new{maximum,size,permuted});
        }
    }
}
