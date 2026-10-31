using BrawlCrate.Studio;
using BrawlCrate.Studio.Retargeting;
using BrawlLib.SSBB.ResourceNodes;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Reflection;
using System.Web.Script.Serialization;
using System.Threading;

internal static partial class Program
{
    static int MappingAudit(string input,string output)
    {
        Directory.CreateDirectory(output);string hash=FighterProject.HashFile(input);
        using(var root=NodeFactory.FromFile(null,input))
        {
            var model=Walk(root).OfType<MDL0Node>().OrderByDescending(m=>m.NumVertices).First();
            var bones=model.AllBones.ToArray();
            var joints=(RigJoint[])typeof(BrawlRetargeter).GetMethod("NativeJoints",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,new object[]{bones});
            var analysis=SkeletonCharacterizer.Analyze(joints);
            var neutral=new Rig("Read-only native audit","Native joints",joints,new Dictionary<string,int>(),1);
            File.WriteAllText(Path.Combine(output,"skeleton.json"),new JavaScriptSerializer().Serialize(new{InputSha256=hash,Model=model.Name,
                Roles=analysis.Roles.ToDictionary(p=>p.Key,p=>joints[p.Value].Name),analysis.Evidence,analysis.Warnings,analysis.Missing,analysis.Unavailable,
                Joints=joints.Select((j,i)=>new{j.Name,j.Parent,Point=neutral.Rest.Point(i),j.Bind}).ToArray()}));
        }
        Check(FighterProject.HashFile(input)==hash,"Read-only native characterization audit preserves original bytes");return 0;
    }
    static int TerminalContactChecks(string output)
    {
        Directory.CreateDirectory(output);
        foreach(bool permuted in new[]{false,true})foreach(float units in new[]{.4f,1.8f})foreach(string side in new[]{"left","right"})
        {
            var full=CoordinateFixture("unfamiliar-full-",1,.3f,2,Quaternion.Identity,true,!permuted).Rig;
            var original=CoordinateFixture("unfamiliar-partial-",units,.3f,5,Quaternion.Identity,true,permuted).Rig;
            var roles=new Dictionary<string,int>(original.Roles);int endpoint=roles[side+".hand"];roles.Remove(side+".hand");
            var joints=original.Joints.Select((j,i)=>new RigJoint(j.Name,j.Parent,i==endpoint?new JointTransform(Vector3.Zero,j.Bind.Rotation,j.Bind.Scale):j.Bind)).ToArray();
            var partial=new Rig("Opaque terminal arm","Exact existing rest evidence",joints,roles,units,Vector3.UnitY);
            Check(SkeletonCharacterizer.HasTerminalArm(partial,partial.Roles,side),"Opaque terminal forearms are proven from existing topology/rest, including coincident helper nodes",new{permuted,units,side});
            SkeletonCharacterizer.Validate(joints,roles);
            foreach(bool reverse in new[]{false,true})
            {
                var source=reverse?partial:full;var target=reverse?full:partial;var plan=new HumanoidRetargeter(source,target);
                var pose=new RigPose(source);int lower=source.Roles[side+".lowerArm"];
                pose.Local[lower].Rotation=Quaternion.Normalize(pose.Local[lower].Rotation*Quaternion.CreateFromAxisAngle(Vector3.UnitZ,.31f));
                pose.Local[0].Translation+=new Vector3(.4f,.2f,-.3f);pose.Update();
                var fk=plan.Evaluate(pose,false).Pose;var corrected=plan.Evaluate(pose).Pose;
                Check(plan.UnavailableEndEffectors.SequenceEqual(new[]{side+".hand"})&&!plan.Mapping.Any(m=>m.Role==side+".hand"),"Either transfer direction explicitly limits unavailable hand capability without inventing a mapped wrist");
                Check(new[]{"upperArm","lowerArm"}.All(r=>corrected.Local[target.Roles[side+"."+r]].Equals(fk.Local[target.Roles[side+"."+r]])),"Incomplete arm retains full mapped forearm/upper-arm FK while other limbs use ordinary correction");
                Check(corrected.Local[0].Equals(fk.Local[0])&&corrected.Local.Select(t=>t.Scale).SequenceEqual(fk.Local.Select(t=>t.Scale)),"Terminal-arm capability preserves source-owned controls and all native scales");
                if(!target.Roles.ContainsKey(side+".hand"))
                {
                    bool refused=false;try{plan.SolveLimb(corrected,new RetargetPose{Pose=corrected},side,true,Vector3.Zero);}catch(InvalidDataException){refused=true;}
                    Check(refused,"Unavailable target hand IK refuses explicitly rather than fabricating an endpoint");
                }
            }
            bool missing=false;try{SkeletonCharacterizer.Validate(original.Joints,roles);}catch(InvalidDataException){missing=true;}
            Check(missing,"A real distinct distal descendant with no reviewed hand binding still requires review");
            var tiny=joints.Select((j,i)=>i==endpoint?new RigJoint(j.Name,j.Parent,new JointTransform(new Vector3(0.0000001f,0,0),j.Bind.Rotation,j.Bind.Scale)):j).ToArray();
            var uncertain=new Rig("Tiny distinct descendant","Unresolved binding",tiny,roles,units,Vector3.UnitY);
            Check(!SkeletonCharacterizer.HasTerminalArm(uncertain,roles,side),"Even a tiny nonzero local descendant is not erased by a scale-dependent absent-hand guess");
            var wrong=new Dictionary<string,int>(roles){{side+".hand",endpoint}};bool zero=false;
            try{SkeletonCharacterizer.Validate(joints,wrong);}catch(InvalidDataException){zero=true;}
            Check(zero,"An explicitly mapped coincident helper is rejected as a zero-length hand segment");
        }
        foreach(bool permuted in new[]{false,true})foreach(float units in new[]{.4f,1.8f})
        {
            var rig=CoordinateFixture("unfamiliar-lock-",units,.2f,4,Quaternion.Identity,true,permuted).Rig;
            foreach(string role in new[]{"left.foot","right.foot","pelvis"})
            {
                int[] influences=role=="pelvis"?new[]{rig.Roles["pelvis"],rig.Roles["spine"],rig.Roles["chest"],rig.Roles["left.upperLeg"],rig.Roles["left.lowerLeg"]}:
                    new[]{rig.Roles["pelvis"],rig.Roles[role.Split('.')[0]+".upperLeg"],rig.Roles[role.Split('.')[0]+".lowerLeg"],rig.Roles[role],rig.Roles[role.Split('.')[0]+".toe"]};
                Vector3 pivot=rig.Rest.Point(role);pivot.Y=0;
                var points=(from x in new[]{-.1f,.1f} from z in new[]{-.2f,.2f} select new SkinPoint{Weights=influences.Select((bone,i)=>{
                    Matrix4x4.Invert(rig.Rest.World[bone],out var inverse);
                    return new SkinWeight{Joint=bone,Weight=new[]{.05f,.05f,.1f,.6f,.2f}[i],Local=Vector3.Transform(pivot+new Vector3(x,0,z)*units,inverse)};
                }).ToArray()}).ToArray();
                rig.Surfaces[role]=new ContactSurface{Role=role,Points=points,NormalLocal=Vector3.Transform(Vector3.UnitY,Quaternion.Inverse(rig.Rest.Orientation[rig.Roles[role]])),ForwardLocal=Vector3.UnitZ};
            }
            var plan=new HumanoidRetargeter(rig,rig);var pose=new RigPose(rig);var controls=rig.Surfaces.ToDictionary(p=>p.Key,p=>{
                var marker=p.Value.Marker(rig.Rest);return new ContactGoal{Support=1,Lock=1,Marker=marker,Anchor=marker.Evaluate(rig.Rest)};
            });
            var weights=rig.Surfaces.Values.SelectMany(s=>s.Points).SelectMany(p=>p.Weights).Select(w=>w.Weight).ToArray();
            pose.SetWorldPoint(rig.Roles["pelvis"],pose.Point("pelvis")+new Vector3(.02f,.005f,-.015f)*units);
            var before=(JointTransform[])pose.Local.Clone();var rotations=new[]{"left.foot","right.foot"}.ToDictionary(r=>r,r=>pose.Orientation[rig.Roles[r]]);
            var contact=new ContactMotion(plan,t=>new RigPose(rig),1,false);
            typeof(ContactMotion).GetMethod("FitBodyContacts",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(contact,new object[]{pose,controls,null,true});
            float length=Vector3.Distance(rig.Rest.Point("left.upperLeg"),rig.Rest.Point("left.lowerLeg"))+Vector3.Distance(rig.Rest.Point("left.lowerLeg"),rig.Rest.Point("left.foot"));
            double drift=controls.Values.Max(c=>{var d=c.Marker.Evaluate(pose)-c.Anchor;d.Y=0;return d.Length();});
            double penetration=rig.Surfaces.Values.Max(s=>Math.Max(0,-s.Clearance(pose)));
            Check(drift<=length*.001&&penetration<=length*.001,"Established full-skin body/foot anchors satisfy the unchanged combined contact bound on unequal opaque/permuted rigs",new{permuted,units,drift,penetration,length});
            Check(rotations.All(r=>SpanAngle(r.Value,pose.Orientation[rig.Roles[r.Key]])<=.25)&&pose.Local[0].Equals(before[0]),"Simultaneous body locking retains end orientation and exact protected root locals");
            Check(weights.SequenceEqual(rig.Surfaces.Values.SelectMany(s=>s.Points).SelectMany(p=>p.Weights).Select(w=>w.Weight))&&pose.Local.Select(t=>t.Scale).SequenceEqual(before.Select(t=>t.Scale)),"Full five-influence contact solving preserves every influence coefficient and scale");
        }
        File.WriteAllText(Path.Combine(output,"terminal-contact-checks.json"),new JavaScriptSerializer().Serialize(new{Passed=true,Checks}));return 0;
    }
    static int NativePairFixture(string sourcePath,string targetPath,string motionPath,string name,string output)
    {
        var inputs=new[]{sourcePath,targetPath,motionPath}.Distinct().ToDictionary(p=>p,FighterProject.HashFile);
        using(var s=NodeFactory.FromFile(null,sourcePath))using(var t=NodeFactory.FromFile(null,targetPath))using(var m=NodeFactory.FromFile(null,motionPath))
        {
            MDL0Node Largest(ResourceNode root)
            {
                var models=Walk(root).OfType<MDL0Node>().OrderByDescending(n=>n.NumVertices).ToArray();
                if(models.Length<1||models.Length>1&&models[0].NumVertices==models[1].NumVertices)throw new InvalidDataException("Ambiguous full-geometry native model: an exact external member is required.");
                return models[0];
            }
            var source=BrawlRetargeter.Capture(Largest(s));var target=BrawlRetargeter.Capture(Largest(t));
            var clip=Walk(m).OfType<CHR0Node>().Single(c=>c.Name==name);
            string Member(ResourceNode n)=>n.Parent==null?"root":Member(n.Parent)+"/"+n.Index;
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(output)));
            File.WriteAllText(output,new JavaScriptSerializer().Serialize(new{Schema="fighterport-authentic-export-fixture-1",SourceNativeMember=Member(clip),
                SourceClipName=name,SourceFrames=clip.FrameCount,SourceVersion=clip.Version,SourceLoop=clip.Loop,
                SourceClipSha256=inputs[motionPath],SourceModelSha256=inputs[sourcePath],TargetModelSha256=inputs[targetPath],
                SourceRigFingerprint=source.Fingerprint,TargetRigFingerprint=target.Fingerprint}));
        }
        Check(inputs.All(p=>FighterProject.HashFile(p.Key)==p.Value),"Exact native pair manifest creation preserves every source/model byte");return 0;
    }
}
