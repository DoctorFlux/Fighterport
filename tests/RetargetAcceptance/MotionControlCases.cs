using BrawlCrate.Studio;
using BrawlCrate.Studio.Retargeting;
using BrawlLib.SSBB.ResourceNodes;
using System;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Web.Script.Serialization;

internal static partial class Program
{
    static int MotionControls(string sourcePath,string targetPath,string output)
    {
        Directory.CreateDirectory(output);
        var hashes=new[]{sourcePath,targetPath}.Distinct().ToDictionary(p=>p,FighterProject.HashFile);
        using(var sr=NodeFactory.FromFile(null,sourcePath))using(var tr=NodeFactory.FromFile(null,targetPath))
        {
            var source=BrawlRetargeter.Capture(Walk(sr).OfType<MDL0Node>().First(n=>!n.Name.ToLowerInvariant().Contains("shadow")));
            var target=BrawlRetargeter.Capture(Walk(tr).OfType<MDL0Node>().First(n=>!n.Name.ToLowerInvariant().Contains("shadow")));
            var plan=new HumanoidRetargeter(source.Rig,target.Rig);
            Check(plan.SourceMotionControls.Length>1 && plan.TargetMotionControls.Length>1,"Native fixtures have separate reviewed body and motion controls");
            foreach(bool authored in new[]{false,true})
            using(var fixture=new CHR0Node{Name=authored?"AuthoredControls":"BodyOnly",FrameCount=9})
            {
                fixture.CreateEntry(source.Rig.Joints[source.Rig.Roles["pelvis"]].Name).SetKeyframe(5,4,17,true);
                var first=plan.Mapping.First(plan.IsTrajectory);
                if(authored)
                {
                    var entry=fixture.CreateEntry(first.Source);
                    entry.SetKeyframe(4,0,0,true)._tangent=90;
                    entry.SetKeyframe(4,8,720,true)._tangent=90;
                    entry.SetKeyframe(6,2,3,true)._tangent=.3f;
                    entry.SetKeyframe(6,6,5,true)._tangent=-.2f;
                    entry.Keyframes[6].SetFrameValue(6,6,true)._tangent=.4f;
                    entry.SetKeyframe(0,0,1,true)._tangent=.025f;
                    entry.SetKeyframe(0,8,1.2f,true)._tangent=.025f;
                }
                using(var baked=BrawlRetargeter.Bake(source,target,fixture,false))
                {
                    string path=Path.Combine(output,fixture.Name+".chr0");baked.Animation.ExportChecked(path);
                    using(var saved=(CHR0Node)NodeFactory.FromFile(null,path))
                    {
                        foreach(int control in plan.TargetMotionControls)
                        {
                            var map=plan.Mapping.SingleOrDefault(m=>m.TargetIndex==control && plan.IsTrajectory(m));
                            var entry=(CHR0EntryNode)saved.FindChild(target.Rig.Joints[control].Name,false);
                            bool own=authored && map!=null && map.SourceIndex==first.SourceIndex;
                            Check(own?entry!=null:entry==null,"Only a control's own source keys authorize a target entry: "+target.Rig.Joints[control].Name+" authored="+authored);
                            foreach(float time in Enumerable.Range(0,33).Select(i=>i/4f).Concat(Enumerable.Range(0,8).SelectMany(i=>new[]{i+.37f,i+.73f})))
                            {
                                var actual=BrawlRetargeter.Sample(target,saved,time).Local[control];
                                var expected=target.Rig.Joints[control].Bind;
                                if(own)
                                {
                                    // This root's parent is the world: explicit source
                                    // channels are independent of anatomy/leg ratio.
                                    Check(source.Rig.Joints[first.SourceIndex].Parent<0 && target.Rig.Joints[control].Parent<0,"Authored root fixture uses world parent coordinates");
                                    var input=(CHR0EntryNode)fixture.FindChild(first.Source,false);
                                    expected.Translation.X+=input.Keyframes[6].GetFrameValue(time)-source.Rig.Joints[first.SourceIndex].Bind.Translation.X;
                                    expected.Scale.X*=input.Keyframes[0].GetFrameValue(time)/source.Rig.Joints[first.SourceIndex].Bind.Scale.X;
                                    expected.Rotation=Quaternion.CreateFromAxisAngle(Vector3.UnitY,input.Keyframes[4].GetFrameValue(time)*(float)Math.PI/180);
                                }
                                var delta=Quaternion.Normalize(actual.Rotation*Quaternion.Inverse(expected.Rotation));
                                double degrees=2*Math.Atan2(new Vector3(delta.X,delta.Y,delta.Z).Length(),Math.Abs(delta.W))*180/Math.PI;
                                Check(Vector3.Distance(actual.Translation,expected.Translation)<=.0002 && Vector3.Distance(actual.Scale,expected.Scale)<=.00001 && degrees<=.01,
                                    "Independent native local control expectation at "+time+" bone="+control+" authored="+authored,new{degrees});
                            }
                            if(own)
                            {
                                Check(entry.Keyframes[4].GetKeyframe(8)._value==720,"Native export retains a source-authored full turn");
                                Check(entry.Keyframes[6].GetKeyframe(6).Second!=null,"Native export retains source duplicate knots");
                            }
                        }
                    }
                }
            }
            foreach(var selected in plan.Mapping.Where(plan.IsTrajectory))
            using(var fixture=new CHR0Node{Name="OwnControl_"+selected.SourceIndex,FrameCount=5})
            {
                var entry=fixture.CreateEntry(selected.Source);
                entry.SetKeyframe(5,0,0,true)._tangent=10;
                entry.SetKeyframe(5,4,40,true)._tangent=10;
                using(var baked=BrawlRetargeter.Bake(source,target,fixture,false))
                {
                    string path=Path.Combine(output,fixture.Name+".chr0");baked.Animation.ExportChecked(path);
                    using(var saved=(CHR0Node)NodeFactory.FromFile(null,path))
                    {
                        Check(plan.TargetMotionControls.All(i=>
                            (saved.FindChild(target.Rig.Joints[i].Name,false)!=null)==(i==selected.TargetIndex)),
                            "Only the selected source control creates a target track, including inherited descendants: "+selected.Source);
                        var actual=(CHR0EntryNode)saved.FindChild(selected.Target,false);
                        Check(actual.Keyframes[5].GetKeyframe(4)._value==40 && actual.Keyframes[5].GetKeyframe(4)._tangent==10 &&
                            Enumerable.Range(0,3).Concat(Enumerable.Range(6,3)).All(c=>actual.Keyframes[c].KeyCount==0),
                            "Each source control retains only its authored rotation group and exact keys: "+selected.Source);
                    }
                }
            }
            // Rename every joint and swap independent sibling storage using a
            // stable reverse-name topological order; the protected role is structural.
            Rig Anonymous(Rig rig)
            {
                var order=Enumerable.Range(0,rig.Joints.Length).OrderBy(i=>Depth(rig,i)).ThenByDescending(i=>i).ToArray();
                var indices=order.Select((old,index)=>new{old,index}).ToDictionary(x=>x.old,x=>x.index);
                return new Rig("anonymous","reviewed",order.Select(i=>new RigJoint("joint_"+indices[i],rig.Joints[i].Parent<0?-1:indices[rig.Joints[i].Parent],rig.Joints[i].Bind)),rig.Roles.ToDictionary(p=>p.Key,p=>indices[p.Value]),rig.HeadRadius);
            }
            var anonymous=new HumanoidRetargeter(Anonymous(source.Rig),Anonymous(target.Rig));
            var pose=new RigPose(anonymous.Source);
            var root=anonymous.Mapping.First(anonymous.IsTrajectory);
            pose.Local[root.SourceIndex].Rotation=Quaternion.CreateFromAxisAngle(Vector3.UnitY,.6f);pose.Update();
            var result=anonymous.Evaluate(pose).Pose;
            foreach(int control in anonymous.TargetMotionControls.Where(i=>i!=root.TargetIndex))
                Check(Math.Abs(Quaternion.Dot(result.Local[control].Rotation,anonymous.Target.Joints[control].Bind.Rotation))>1-1e-6,"Renamed/permuted child control does not compensate inherited rotation");

            // Each reviewed ancestor owns its translation independently of the
            // character's limb proportions and its parent's rest-coordinate
            // basis. Child controls have no authored delta in these fixtures.
            Rig MotionVariant(Rig rig,float legRatio,float phase)
            {
                var legEnds=new[]{"left.lowerLeg","left.foot","right.lowerLeg","right.foot"}.Select(r=>rig.Roles[r]).ToArray();
                var basis=Enumerable.Range(0,rig.Joints.Length).Select(i=>Quaternion.CreateFromYawPitchRoll(phase+i*.13f,phase*-.7f+i*.09f,phase*.4f-i*.11f)).ToArray();
                var joints=rig.Joints.Select((j,i)=>
                {
                    var bind=j.Bind;if(legEnds.Contains(i))bind.Translation*=legRatio;
                    return new RigJoint(j.Name,j.Parent,RebaseLocal(bind,j.Parent,i,basis));
                });
                return Anonymous(new Rig("anonymous motion proportions","reviewed",joints,rig.Roles,rig.HeadRadius));
            }
            foreach(float factor in new[]{.65f,1.6f})
            {
                var changed=new HumanoidRetargeter(MotionVariant(source.Rig,factor,.37f),MotionVariant(target.Rig,1/factor,-.51f));
                Check(Math.Abs(changed.LegScale-plan.LegScale)>.01,"Motion-control fixture changes anatomy without changing gameplay units",new{factor,changed.LegScale,Original=plan.LegScale});
                foreach(var selected in changed.Mapping.Where(changed.IsTrajectory))
                foreach(var axis in new[]{Vector3.UnitX,Vector3.UnitY,Vector3.UnitZ})
                {
                    var own=new RigPose(changed.Source);var delta=axis*3.25f;
                    own.Local[selected.SourceIndex].Translation+=delta;own.Update();
                    var transferred=changed.Evaluate(own,false).Pose;
                    int sourceParent=changed.Source.Joints[selected.SourceIndex].Parent,targetParent=changed.Target.Joints[selected.TargetIndex].Parent;
                    var sourceMatrix=sourceParent<0?Matrix4x4.Identity:Matrix4x4.CreateFromQuaternion(changed.Source.Rest.Orientation[sourceParent]);
                    var targetMatrix=targetParent<0?Matrix4x4.Identity:Matrix4x4.CreateFromQuaternion(changed.Target.Rest.Orientation[targetParent]);
                    if(sourceParent>=0)Check(Math.Abs(changed.Source.Rest.Orientation[sourceParent].W)<.999f && Math.Abs(changed.Target.Rest.Orientation[targetParent].W)<.999f,"Own translation is tested under nonneutral reviewed parent bases");
                    Check(Matrix4x4.Invert(targetMatrix,out Matrix4x4 targetInverse),"Independent reviewed parent basis is invertible");
                    // Matrix FK is the oracle, independent of the production
                    // quaternion delta-transfer helper and anatomical scaling.
                    var worldDelta=Vector3.TransformNormal(delta,sourceMatrix);
                    var expectedDelta=Vector3.TransformNormal(worldDelta,targetInverse);
                    foreach(int control in changed.TargetMotionControls)
                    {
                        var bind=changed.Target.Joints[control].Bind;var actual=transferred.Local[control];
                        var expected=bind.Translation+(control==selected.TargetIndex?expectedDelta:Vector3.Zero);
                        var rotation=Quaternion.Normalize(actual.Rotation*Quaternion.Inverse(bind.Rotation));
                        double angle=2*Math.Atan2(new Vector3(rotation.X,rotation.Y,rotation.Z).Length(),Math.Abs(rotation.W))*180/Math.PI;
                        Check(Vector3.Distance(actual.Translation,expected)<=.0002 && Vector3.Distance(actual.Scale,bind.Scale)<=.00001 && angle<=.01,
                            "Each reviewed motion ancestor transfers only its own XYZ translation; unauthored child locals retain bind",new{factor,Selected=selected.Role,Control=control,Axis=axis.ToString(),angle});
                    }
                    var actualDelta=transferred.Local[selected.TargetIndex].Translation-changed.Target.Joints[selected.TargetIndex].Bind.Translation;
                    Check(Vector3.Distance(Vector3.TransformNormal(actualDelta,targetMatrix),worldDelta)<=.0002 && Math.Abs(actualDelta.Length()-delta.Length())<=.0002,
                        "Reviewed gameplay translation retains source units beneath differing nonneutral parent bases",new{factor,Selected=selected.Role,Axis=axis.ToString()});
                }
            }
        }
        Check(hashes.All(p=>FighterProject.HashFile(p.Key)==p.Value),"Authentic native model source bytes remain immutable");
        File.WriteAllText(Path.Combine(output,"motion-controls.json"),new JavaScriptSerializer{MaxJsonLength=16000000}.Serialize(new{Passed=true,Policy=HumanoidRetargeter.MotionControlPolicy,Checks}));
        Console.WriteLine("PASS "+Checks.Count+" motion control checks");return 0;
    }
    static int Depth(Rig rig,int index){int depth=0;for(int i=rig.Joints[index].Parent;i>=0;i=rig.Joints[i].Parent)depth++;return depth;}
}
