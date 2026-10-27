using BrawlCrate.Studio;
using BrawlCrate.Studio.Retargeting;
using BrawlLib.SSBB.ResourceNodes;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;

internal static partial class Program
{
    private static void Rejected(Action action, string reason, string name)
    {
        try { action(); }
        catch (InvalidDataException e) { Check(e.Message.IndexOf(reason, StringComparison.OrdinalIgnoreCase) >= 0, name, e.Message); return; }
        throw new InvalidOperationException("Expected validation failure: " + name);
    }
    private static Rig Rebase(Rig rig, out Quaternion[] basis)
    {
        basis = Enumerable.Range(0, rig.Joints.Length).Select(i => Quaternion.CreateFromYawPitchRoll(i * 0.13f, i * -0.07f, i * 0.11f)).ToArray();
        var localBasis = basis;
        var joints = rig.Joints.Select((j, i) => new RigJoint(j.Name, j.Parent, RebaseLocal(j.Bind, j.Parent, i, localBasis)));
        return new Rig(rig.Name, rig.Profile, joints, rig.Roles, rig.HeadRadius, rig.CoordinateUp, rig.CoordinateFrame);
    }
    private static JointTransform RebaseLocal(JointTransform value, int parent, int index, Quaternion[] basis)
    {
        Quaternion inverseParent = parent < 0 ? Quaternion.Identity : Quaternion.Inverse(basis[parent]);
        return new JointTransform(Vector3.Transform(value.Translation, inverseParent), inverseParent * value.Rotation * basis[index], value.Scale);
    }
    private static void AdditionalRigCases(BrawlRig source, BrawlRig target, CHR0Node clip)
    {
        var partialPose=typeof(RigPose).GetConstructor(System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic,null,
            new[]{typeof(RigPose),typeof(JointTransform[]),typeof(int[])},null);
        bool partialEqual=true;
        foreach(Rig rig in new[]{source.Rig,target.Rig})foreach(int joint in new[]{rig.Roles["pelvis"],rig.Roles["left.upperArm"],rig.Roles["right.hand"],rig.Joints.Length-1})
        {
            var reference=new RigPose(rig);var local=(JointTransform[])reference.Local.Clone();
            local[joint].Rotation=Quaternion.CreateFromYawPitchRoll(0.17f,-0.13f,0.11f)*local[joint].Rotation;
            local[joint].Scale*=new Vector3(0.8f,1.1f,1.2f);
            var expected=new RigPose(rig,local);
            var actual=(RigPose)partialPose.Invoke(new object[]{reference,local,new[]{joint}});
            partialEqual&=expected.World.SequenceEqual(actual.World) && expected.Orientation.SequenceEqual(actual.Orientation);
        }
        Check(partialEqual,"Incremental curve FK is identical to independent full FK, including nonuniform scale and separate branches");
        foreach(var pair in new[]{Tuple.Create(source.Rig,target.Rig),Tuple.Create(target.Rig,source.Rig)})
        {
            Rig rig=pair.Item1;var hanging=new RigPose(rig);int freeArm=rig.Roles["right.upperArm"];
            hanging.SetWorldRotation(freeArm,RigMath.FromTo(hanging.Point("right.hand")-hanging.Point(freeArm),-rig.GroundNormal)*hanging.Orientation[freeArm]);
            hanging.SetWorldPoint(rig.Roles["pelvis"],hanging.Point("pelvis")+rig.GroundNormal*(rig.GroundHeight-Vector3.Dot(hanging.Point("left.hand"),rig.GroundNormal)));
            var supportPlan=new HumanoidRetargeter(rig,pair.Item2);
            var support=new ContactMotion(supportPlan,t=>t<8?hanging:rig.Rest,24,true,requireFloorEvidence:true);
            Check(support.Suspended!=null && support.GripWeight("left",0)>0.99f && support.GripWeight("right",0)==0,
                "Automatic suspended support identifies a single raised hand without pinning the free arm");
            var grip=support.Evaluate(0).Pose;
            Check(Vector3.Distance(grip.Point("left.hand"),support.Suspended.GripGoal(hanging,"left"))<0.03f,
                "One-hand support fits target proportions through pelvis/arm correction");
            var standing=support.Evaluate(24).Pose;
            Check(support.StabilizedStandingFeet.Length==2 && new[]{"left","right"}.All(side=>Math.Abs(pair.Item2.Surfaces[side+".foot"].Clearance(standing))<0.03f),
                "A later standing segment receives hip/foot fitting despite an earlier suspended phase");
        }
        var contactStep=typeof(ContactMotion).GetMethod("SolveContactSystem",System.Reflection.BindingFlags.Static|System.Reflection.BindingFlags.NonPublic);
        double stepError=0;
        foreach(int count in new[]{9,90})
        {
            int rows=count==9?3:18;
            double[][] jacobian=Enumerable.Range(0,rows).Select(r=>Enumerable.Range(0,count).Select(i=>Math.Sin((r+1)*(i+3)*0.37)).ToArray()).ToArray();
            double[] expected=Enumerable.Range(0,count).Select(i=>Math.Cos(i*0.43)*0.01).ToArray();
            double[] diagonal=Enumerable.Range(0,count).Select(i=>Math.Pow(10,i%7-4)).ToArray();
            var normal=new double[count,count];
            for(int i=0;i<count;i++)for(int j=0;j<count;j++)normal[i,j]=(i==j?diagonal[i]:0)+Enumerable.Range(0,rows).Sum(r=>jacobian[r][i]*jacobian[r][j]);
            double[] rhs=Enumerable.Range(0,count).Select(i=>Enumerable.Range(0,count).Sum(j=>normal[i,j]*expected[j])).ToArray();
            var actual=(double[])contactStep.Invoke(null,new object[]{normal,rhs});
            stepError=Math.Max(stepError,actual.Zip(expected,(a,b)=>Math.Abs(a-b)).Max());
        }
        Check(stepError<0.000001,"Coupled contact solve satisfies independently constructed regularized joint equations",new{MaximumError=stepError});
        var batchStep=typeof(ContactMotion).GetMethod("ApplyBodyStep",System.Reflection.BindingFlags.Static|System.Reflection.BindingFlags.NonPublic);
        double batchError=0;
        foreach(Rig rig in new[]{source.Rig,target.Rig})
        {
            var slow=new RigPose(rig);var fast=new RigPose(rig);
            int pelvis=rig.Roles["pelvis"];
            var controls=rig.Roles.Where(p=>p.Key=="pelvis" || p.Key=="spine" || p.Key=="chest" || p.Key=="neck" || p.Key=="head" || p.Key.Contains(".")).Select(p=>p.Value).Distinct().OrderBy(i=>i).ToArray();
            var map=Enumerable.Repeat(-1,rig.Joints.Length).ToArray();for(int i=0;i<controls.Length;i++)map[controls[i]]=i;
            var turns=controls.Select((b,i)=>new Vector3((float)Math.Sin(i)*0.03f,(float)Math.Cos(i)*0.02f,0.01f)).ToArray();
            var shift=new Vector3(0.03f,-0.02f,0.01f);
            slow.SetWorldPoint(pelvis,slow.Point(pelvis)+shift*0.5f);
            for(int i=0;i<controls.Length;i++){float angle=turns[i].Length();slow.SetWorldRotation(controls[i],Quaternion.CreateFromAxisAngle(turns[i]/angle,angle*0.5f)*slow.Orientation[controls[i]]);}
            batchStep.Invoke(null,new object[]{fast,(JointTransform[])fast.Local.Clone(),pelvis,map,shift,turns,0.5f});
            batchError=Math.Max(batchError,Enumerable.Range(0,rig.Joints.Length).Max(i=>Vector3.Distance(slow.Point(i),fast.Point(i))));
        }
        Check(batchError<0.0001,"Batched body controls preserve independently evaluated sequential world-space FK",new{MaximumError=batchError});
        var limbStep=typeof(HumanoidRetargeter).GetMethod("SolveLimb",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic);
        var bendPlan=new HumanoidRetargeter(source.Rig,target.Rig);double bendDot=1;
        foreach(string side in new[]{"left","right"})
        {
            var pose=new RigPose(target.Rig);int hip=target.Rig.Roles[side+".upperLeg"],knee=target.Rig.Roles[side+".lowerLeg"],foot=target.Rig.Roles[side+".foot"];
            pose.SetWorldRotation(hip,RigMath.FromTo(pose.Point(knee)-pose.Point(hip),-Vector3.UnitY)*pose.Orientation[hip]);
            pose.SetWorldRotation(knee,RigMath.FromTo(pose.Point(foot)-pose.Point(knee),Vector3.Normalize(new Vector3(0.004f,-1,0)))*pose.Orientation[knee]);
            Vector3 axis=Vector3.Normalize(pose.Point(foot)-pose.Point(hip)),bend=pose.Point(knee)-pose.Point(hip);bend-=axis*Vector3.Dot(bend,axis);bend=Vector3.Normalize(bend);
            float length=Vector3.Distance(pose.Point(hip),pose.Point(knee))+Vector3.Distance(pose.Point(knee),pose.Point(foot));
            Vector3 goal=pose.Point(foot)+bend*length*0.025f;
            limbStep.Invoke(bendPlan,new object[]{pose,new RetargetPose(),side,false,goal,null});
            axis=Vector3.Normalize(pose.Point(foot)-pose.Point(hip));Vector3 actual=pose.Point(knee)-pose.Point(hip);actual-=axis*Vector3.Dot(actual,axis);
            Vector3 expected=bend-axis*Vector3.Dot(bend,axis);
            bendDot=Math.Min(bendDot,Vector3.Dot(Vector3.Normalize(actual),Vector3.Normalize(expected)));
        }
        Check(bendDot>0.99,"Nearly straight limbs retain their transverse bend when the endpoint goal shifts",new{MinimumPoleDot=bendDot});
        foreach(Rig rig in new[]{source.Rig,target.Rig})
        {
            Vector3 originalNormal=rig.GroundNormal;double supportError=0;int queries=0;bool sameVertices=true;
            var lowest=typeof(ContactSurface).GetMethod("LowestPoint",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic);
            try
            {
                for(int phase=0;phase<4;phase++)
                {
                    var pose=new RigPose(rig);
                    int root=Array.FindIndex(rig.Joints,j=>j.Parent<0);
                    if(phase>0)
                    {
                        pose.Local[root].Scale*=new Vector3(0.7f,1.4f,0.85f);
                        pose.Update();pose.SetWorldRotation(root,Quaternion.CreateFromYawPitchRoll(phase*0.6f,phase*0.17f,phase*-0.21f)*pose.Orientation[root]);
                        pose.SetWorldPoint(rig.Roles["pelvis"],pose.Point("pelvis")+new Vector3(phase*0.3f,phase*-0.2f,phase==3?10000:0.1f));
                        foreach(string role in new[]{"left.lowerArm","right.lowerLeg","neck"})
                            pose.SetWorldRotation(rig.Roles[role],Quaternion.CreateFromYawPitchRoll(phase*0.1f,0.2f,-0.1f)*pose.Orientation[rig.Roles[role]]);
                    }
                    foreach(Vector3 normal in new[]{Vector3.UnitY,Vector3.UnitX,Vector3.UnitZ,Vector3.Normalize(new Vector3(0.4f,0.8f,-0.3f))})
                    {
                        rig.GroundNormal=normal;
                        foreach(var surface in rig.Surfaces.Values)
                        {
                            // Materialize float heights: x86 can otherwise keep
                            // one repeated expression in extended precision.
                            float[] heights=surface.Points.Select(p=>Vector3.Dot(p.Evaluate(pose),normal)).ToArray();
                            float minimum=heights.Min();
                            var actual=(SkinPoint)lowest.Invoke(surface,new object[]{pose,0f});
                            double tolerance=Math.Max(0.000001,Math.Abs(minimum)*0.00000025);
                            double error=Math.Abs(surface.Clearance(pose)-(minimum-rig.GroundHeight));
                            sameVertices&=surface.Points.Contains(actual) && error<=tolerance;
                            supportError=Math.Max(supportError,error);queries++;
                        }
                    }
                }
            }
            finally{rig.GroundNormal=originalNormal;}
            Check(sameVertices,"Skin support bounds retain original minima within float precision under pose, normal and scale changes: "+rig.Name,new{Queries=queries,MaximumError=supportError,OriginalVertices=sameVertices});
        }
        {
            var rig=new Rig("support ties","neutral",new[]{new RigJoint("joint",-1,new JointTransform(Vector3.Zero,Quaternion.Identity,Vector3.One))},new Dictionary<string,int>(),1);
            var surface=new ContactSurface{Role="head",Points=Enumerable.Range(0,64).Select(i=>new SkinPoint{Weights=new[]{new SkinWeight{Joint=0,Weight=1,Local=new Vector3(i%8,i==0||i==63?-1:0,i/8)}}}).ToArray()};
            var lowest=typeof(ContactSurface).GetMethod("LowestPoint",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic);
            Check(ReferenceEquals(surface.Points[0],lowest.Invoke(surface,new object[]{rig.Rest,0f})),"Exact support-height ties retain the original vertex order");
        }
        Rejected(() => { using (var unused = BrawlRetargeter.Bake(target, source, clip)) {} }, "bind translations", "Wrong donor skeleton is rejected before writing a motion");
        var roles = new Dictionary<string,int>(target.Rig.Roles); roles.Remove("left.hand");
        var missing = new Rig("missing", "test", target.Rig.Joints, roles, target.Rig.HeadRadius);
        Rejected(() => new HumanoidRetargeter(source.Rig, missing), "missing", "Missing required semantic role is rejected");
        roles = new Dictionary<string,int>(target.Rig.Roles); roles["left.hand"] = roles["left.foot"];
        Rejected(() => new Rig("ambiguous", "test", target.Rig.Joints, roles, target.Rig.HeadRadius), "distinct", "Ambiguous role assignment is rejected");
        int hand = target.Rig.Roles["left.hand"];
        var joints = target.Rig.Joints.Select((j,i) => i == hand ? new RigJoint(j.Name, target.Rig.Roles["chest"], j.Bind) : j).ToArray();
        var incompatible = new Rig("hierarchy", "test", joints, target.Rig.Roles, target.Rig.HeadRadius);
        Rejected(() => new HumanoidRetargeter(source.Rig, incompatible), "hierarchy", "Incompatible mapped ancestry is rejected");
        Rig a = Rebase(source.Rig, out Quaternion[] sourceBasis), b = Rebase(target.Rig, out Quaternion[] targetBasis);
        var original = new HumanoidRetargeter(source.Rig, target.Rig); var rebased = new HumanoidRetargeter(a,b);
        bool cancelled=false;
        try{using(var unused=BrawlRetargeter.Bake(source,target,clip,true,new System.Threading.CancellationToken(true))) { }}
        catch(OperationCanceledException){cancelled=true;}
        Check(cancelled,"Cancelled retarget aborts before creating a native output");
        cancelled=false;
        using(var cancellation=new System.Threading.CancellationTokenSource())
        {
            try{using(var unused=BrawlRetargeter.Bake(source,target,clip,true,cancellation.Token,(percent,message)=>{if(percent>=5)cancellation.Cancel();})) { }}
            catch(OperationCanceledException){cancelled=true;}
        }
        Check(cancelled,"Cancellation during real contact analysis or mapped sampling discards the partial native motion");
        double maximum = 0;
        foreach (float time in new[] {0f, 2.37f, 8.6f, clip.FrameCount - 1f})
        {
            RigPose pose = BrawlRetargeter.Sample(source, clip, time);
            var changed = new RigPose(a, pose.Local.Select((v,i) => RebaseLocal(v, a.Joints[i].Parent, i, sourceBasis)).ToArray());
            RigPose expected = original.Evaluate(pose).Pose, actual = rebased.Evaluate(changed).Pose;
            maximum = Math.Max(maximum, Enumerable.Range(0,b.Joints.Length).Max(i => Vector3.Distance(expected.Point(i),actual.Point(i))));
        }
        Check(maximum < 0.005, "World bind calibration is invariant to independently rotated source and target local bone bases", new {MaximumError=maximum});
        // Repeat arbitrary scrubs, including backwards, to detect hidden solver state.
        var first = new Dictionary<float,Vector3[]>();
        foreach (float time in new[] {9.37f, 0f, 4.12f, 12f, 9.37f, 4.12f, 0f})
        {
            var points = original.Evaluate(BrawlRetargeter.Sample(source,clip,time)).Pose.World.Select(m => m.Translation).ToArray();
            if (first.TryGetValue(time,out Vector3[] previous)) Check(points.SequenceEqual(previous), "Random-access scrub is deterministic at " + time);
            else first.Add(time,points);
        }
        RigPose authoredFk=original.Evaluate(BrawlRetargeter.Sample(source,clip,8.37f),false).Pose;
        var bind = new Dictionary<int,float>();
        foreach (string side in new[] {"left","right"}) foreach (string role in new[] {"lowerArm","hand","lowerLeg","foot"})
        { int i=target.Rig.Roles[side+"."+role]; bind[i]=Vector3.Distance(authoredFk.Point(i),authoredFk.Point(target.Rig.Joints[i].Parent)); }
        RigPose posed=original.Evaluate(BrawlRetargeter.Sample(source,clip,8.37f)).Pose;
        double lengthError=bind.Max(p => Math.Abs(Vector3.Distance(posed.Point(p.Key),posed.Point(target.Rig.Joints[p.Key].Parent))-p.Value));
        Check(lengthError < 0.002, "IK preserves target limb segment lengths including the clip's authored translation deltas",new {MaximumError=lengthError});
        // A source/target profile names bones; solver math must not depend on
        // Mario/Snake names or on one particular transfer direction.
        Rig renamedSource=RenameRig(source.Rig,"SourceJoint"),renamedTarget=RenameRig(target.Rig,"TargetJoint");
        var renamedPlan=new HumanoidRetargeter(renamedSource,renamedTarget);
        var standardContact=new ContactMotion(original,t=>BrawlRetargeter.Sample(source,clip,t),clip.FrameCount-1,true);
        var renamedContact=new ContactMotion(renamedPlan,t=>new RigPose(renamedSource,BrawlRetargeter.Sample(source,clip,t).Local),clip.FrameCount-1,true);
        double nameError=0;
        foreach(float time in new[]{0f,3.37f,8.5f,clip.FrameCount-1f})
        {
            RigPose expected=standardContact.Evaluate(time).Pose,actual=renamedContact.Evaluate(time).Pose;
            nameError=Math.Max(nameError,Enumerable.Range(0,target.Rig.Joints.Length).Max(i=>Vector3.Distance(expected.Point(i),actual.Point(i))));
        }
        Check(nameError<0.001,"Mapped FK/IK, proportions and skin contacts are invariant to arbitrary bone and profile names",new{MaximumError=nameError});
        var core=new HashSet<string>{"root","pelvis","spine","chest","neck","head"};
        foreach(string side in new[]{"left","right"})foreach(string part in new[]{"upperArm","lowerArm","hand","upperLeg","lowerLeg","foot"})core.Add(side+"."+part);
        Rig minimalSource=CoreRig(renamedSource,core),minimalTarget=CoreRig(renamedTarget,core);
        var minimalPlan=new HumanoidRetargeter(minimalSource,minimalTarget);
        var coreContacts=new ContactMotion(minimalPlan,t=>new RigPose(minimalSource,BrawlRetargeter.Sample(source,clip,t).Local),clip.FrameCount-1,true);
        double coreHeight=0;
        foreach(float time in new[]{0f,3.37f,8.5f,clip.FrameCount-1f})
        {
            var p=coreContacts.Evaluate(time).Pose;
            foreach(string role in new[]{"left.foot","right.foot"})if(coreContacts.At(role,time).Support>0.99f)
                coreHeight=Math.Max(coreHeight,Math.Abs(minimalTarget.Surfaces[role].Clearance(p)));
        }
        Check(minimalPlan.Mapping.Count(m=>core.Contains(m.Role))==18 && minimalPlan.Mapping.Where(m=>!core.Contains(m.Role)).All(minimalPlan.IsTrajectory) && coreHeight<0.03,"Core humanoid FK/IK works without named helper roles; any inferred helpers follow the ancestor topology",new{Mapping=minimalPlan.Mapping.Length,MaximumFootHeight=coreHeight});
    }

    private static Rig CoreRig(Rig original,HashSet<string> roles)
    {
        var rig=new Rig(original.Name,"Core humanoid",original.Joints,original.Roles.Where(p=>roles.Contains(p.Key)).ToDictionary(p=>p.Key,p=>p.Value),original.HeadRadius,original.CoordinateUp,original.CoordinateFrame);
        rig.GroundHeight=original.GroundHeight;rig.GroundNormal=original.GroundNormal;
        foreach(var p in original.Surfaces)rig.Surfaces.Add(p.Key,p.Value);
        return rig;
    }

    private static Rig RenameRig(Rig original,string prefix)
    {
        var rig=new Rig(prefix,"Mapped humanoid",original.Joints.Select((j,i)=>new RigJoint(prefix+i,j.Parent,j.Bind)),original.Roles,original.HeadRadius,original.CoordinateUp,original.CoordinateFrame);
        rig.GroundHeight=original.GroundHeight;rig.GroundNormal=original.GroundNormal;
        foreach(var pair in original.Surfaces)rig.Surfaces.Add(pair.Key,pair.Value);
        return rig;
    }

    private static void AdditionalNativeCases(MDL0Node source, MDL0Node target, CHR0Node clip, string template, string output, string mario)
    {
        using (var result=BrawlRetargeter.Bake(source,target,clip))
        {
            string donorCopy=Path.Combine(output,"donor-reference.pac");
            File.Copy(Path.Combine(mario,"FitMario00.pac"),donorCopy);
            string projectPath=Path.Combine(output,"Generated.fpstudio");
            using (var project=FighterProject.Create("Snake retarget persistence"))
            {
                project.GroundMode=GroundContactMode.Airborne;
                project.Model=project.StageAsset(template.Replace("FitSnakeMotionEtc.pac","FitSnake00.pac"),"model");
                var donor=project.StageAsset(donorCopy,"model");
                var motion=project.StageAsset(Path.Combine(mario,"FitMarioMotionEtc.pac"),"animation"); project.Animations.Add(motion);
                var generated=project.CreateAnimation("Attack11 (Mario to Snake).chr0",result.Animation.ExportChecked);
                generated.RigIdentity=result.TargetRig;
                generated.Retarget=new RetargetProvenance {Algorithm=HumanoidRetargeter.Algorithm,SourceModelPath=donor.SourcePath,SourceModelSha256=donor.SourceSha256,
                    SourceRig=result.SourceRig,SourceAnimationId=motion.Id,SourceClip=clip.Name+"|"+clip.TreePath,FromProfile=source.Name,ToProfile=target.Name,
                    LegScale=result.Plan.LegScale,ArmScale=result.Plan.ArmScale,Mapping=result.Plan.Mapping.Select(m=>new RetargetBoneBinding {Role=m.Role,Source=m.Source,Target=m.Target}).ToList()};
                project.Animations.Add(generated); project.SelectedAnimation=generated.Id+"|"+clip.Name; project.Frame=12;
                project.Save(projectPath);
            }
            File.Delete(donorCopy); // Our disposable reference copy only, never an original.
            using(var project=FighterProject.Open(projectPath))
            {
                Check(project.GroundMode==GroundContactMode.Airborne,"Next-retarget ground/air option persists independently of saved generated motions");
                var generated=project.Animations.Single(a=>a.Generated);
                using(var decoded=(CHR0Node)NodeFactory.FromFile(null,generated.WorkingPath))
                {
                    Check(project.Warnings.Count==0 && generated.Retarget.Mapping.Count==47 && project.Frame==12 && decoded.Children.Count==70,
                        "Generated native motion and mapping reopen without a source-model reference or companion manifest");
                    var originalPoints=new List<Vector3[]>();
                    foreach(float time in new[] {0f,3.37f,8.37f,12f}) { target.ApplyCHR(decoded,time+1); originalPoints.Add(Vertices(target)); }
                    // Native editing of an unrelated root key must retain authored interpolation on other tracks.
                    var root=(CHR0EntryNode)decoded.FindChild("TopN",false);
                    root.SetKeyframe(6,0,root.GetFrameValue(6,0),true);
                    string second=Path.Combine(output,"reopened-edited.chr0"); decoded.ExportChecked(second);
                    using(var edited=(CHR0Node)NodeFactory.FromFile(null,second))
                    {
                        double error=0; int index=0;
                        foreach(float time in new[] {0f,3.37f,8.37f,12f}) { target.ApplyCHR(edited,time+1); error=Math.Max(error,Vertices(target).Zip(originalPoints[index++],(x,y)=>Vector3.Distance(x,y)).Max()); }
                        Check(error<0.06,"Reopen, native edit and re-export retain fractional interpolation",new {MaximumSkinError=error});
                    }
                }
            }
            string pac=Path.Combine(output,"FitSnakeMotionEtc-retargeted.pac");
            BrawlRetargeter.ExportMotionPac(template,pac,target,result.Animation);
            using (ResourceNode original=NodeFactory.FromFile(null,template))
            using (ResourceNode saved=NodeFactory.FromFile(null,pac))
            {
                var a=Walk(original).ToList().OfType<CHR0Node>().ToDictionary(c=>c.TreePath);
                var b=Walk(saved).ToList().OfType<CHR0Node>().ToDictionary(c=>c.TreePath);
                Check(a.Count==355 && b.Count==355 && a.Keys.All(b.ContainsKey),"New target motion PAC retains all 355 native slots and paths");
                var untouched=a.Values.Where(c=>c.Name!=clip.Name).ToArray();
                bool same=untouched.All(c => c.FrameCount==b[c.TreePath].FrameCount && c.Loop==b[c.TreePath].Loop && Signature(c)==Signature(b[c.TreePath]));
                Check(same,"Every untouched Snake CHR0 slot retains its native interpolation samples",new {Slots=untouched.Length});
                var baked=b.Values.Single(c=>c.Name==clip.Name);
                Check(baked.FrameCount==clip.FrameCount && baked.Children.Any(c=>c.Name=="BodyN") && baked.Children.All(c=>c.Name!="WaistN"),"Matching native PAC slot contains Snake retarget tracks with Mario timing");
                double skin=0;
                foreach(float time in new[] {0f,3.37f,8f,12.5f,clip.FrameCount-1f})
                { target.ApplyCHR(baked,time+1); var actual=Vertices(target); target.ApplyCHR(result.Animation,time+1); skin=Math.Max(skin,actual.Zip(Vertices(target),(x,y)=>Vector3.Distance(x,y)).Max()); }
                Check(skin<0.06,"Fresh PAC decoding preserves native skinned playback",new {MaximumSkinError=skin});
            }
            bool rejected=false; string hash=FighterProject.HashFile(pac);
            try { BrawlRetargeter.ExportMotionPac(template,pac,target,result.Animation); } catch(IOException) { rejected=true; }
            Check(rejected && hash==FighterProject.HashFile(pac),"Native PAC exporter refuses to overwrite existing output");
            Rejected(()=>BrawlRetargeter.ExportMotionPac(Path.Combine(mario,"FitMarioMotionEtc.pac"),Path.Combine(output,"wrong-template.pac"),target,result.Animation),"different skeleton","Mario PAC cannot masquerade as a Snake native target template");
        }
        using (var turn=new CHR0Node {Name="NumericSpinRegression",FrameCount=41})
        {
            foreach(string name in new[] {"TopN","HipN","BustN"})
            {
                var bone=source.AllBones.Single(b=>b.Name==name); var entry=turn.CreateEntry(name);
                for(int c=0;c<9;c++) entry.SetKeyframe(c,0,bone.BindState[c],true)._tangent=0;
                if(name=="TopN") { entry.SetKeyframe(4,0,0,true)._tangent=18; entry.SetKeyframe(4,40,720,true)._tangent=18; }
            }
            using(var result=BrawlRetargeter.Bake(source,target,turn))
            {
                var root=(CHR0EntryNode)result.Animation.FindChild("TopN",false);
                Check(Enumerable.Range(3,3).Max(c=>Math.Abs(root.GetFrameValue(c,40)-root.GetFrameValue(c,0)))>700,"Continuous Euler baking preserves two full turns instead of collapsing quaternion winding",
                    Enumerable.Range(0,9).Select(i=> new {Frame=i*5, X=root.GetFrameValue(3,i*5),Y=root.GetFrameValue(4,i*5),Z=root.GetFrameValue(5,i*5)}).ToArray());
            }
        }
    }
    private static string Signature(CHR0Node clip)
    {
        return string.Join("|",clip.Children.OfType<CHR0EntryNode>().Select(e=>e.Name+":"+string.Join(";",Enumerable.Range(0,9).Select(c=>
            string.Join(",",Enumerable.Range(0,clip.FrameCount).Select(i=>e.GetFrameValue(c,i+0.37f).ToString("R")))))));
    }
}
