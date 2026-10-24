using BrawlCrate.Studio.Retargeting;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;

internal static partial class Program
{
    private static void NativeAnonymousCharacterization(BrawlLib.SSBB.ResourceNodes.MDL0Node sourceModel,BrawlLib.SSBB.ResourceNodes.MDL0Node targetModel,
        BrawlLib.SSBB.ResourceNodes.CHR0Node idle,string output)
    {
        var sourceBones=sourceModel.AllBones.ToArray();var targetBones=targetModel.AllBones.ToArray();
        var sourceNames=sourceBones.Select(b=>b.Name).ToArray();var targetNames=targetBones.Select(b=>b.Name).ToArray();
        var entries=idle.Children.ToArray();var trackNames=entries.Select(e=>e.Name).ToArray();
        string sourceName=sourceModel.Name,targetName=targetModel.Name,clipName=idle.Name;
        var translate=sourceNames.Select((name,i)=>new{name,Index=i}).ToDictionary(p=>p.name,p=>"anonymous"+p.Index);
        try
        {
            for(int i=0;i<sourceBones.Length;i++)sourceBones[i].Name="anonymous"+i;
            for(int i=0;i<targetBones.Length;i++)targetBones[i].Name="destinationJoint"+i;
            for(int i=0;i<entries.Length;i++)if(translate.TryGetValue(trackNames[i],out string name))entries[i].Name=name;
            sourceModel.Name="SourceModelA";targetModel.Name="TargetModelB";idle.Name="UnrelatedMotion";
            var source=BrawlRetargeter.Capture(sourceModel);var target=BrawlRetargeter.Capture(targetModel);
            using(var result=BrawlRetargeter.Bake(source,target,idle))
            {
                string path=Path.Combine(output,"anonymous-idle-native.chr0");result.Animation.ExportChecked(path);
                using(var saved=(BrawlLib.SSBB.ResourceNodes.CHR0Node)BrawlLib.SSBB.ResourceNodes.NodeFactory.FromFile(null,path))
                {
                    double error=0;
                    foreach(float time in new[]{0f,3.37f,12f,idle.FrameCount-1f})
                    {
                        var before=BrawlRetargeter.Sample(target,result.Animation,time);var after=BrawlRetargeter.Sample(target,saved,time);
                        error=Math.Max(error,Enumerable.Range(0,targetBones.Length).Max(i=>Vector3.Distance(before.Point(i),after.Point(i))));
                    }
                    Check(result.Contacts.FloorEnabled && result.Contacts.PlantedFootSamples>0 && saved.Children.All(c=>c.Name.StartsWith("destinationJoint",StringComparison.Ordinal)) && error<0.06,
                        "Real native idle retarget/export/reopen works with anonymous model, bone and animation names and no supplied map",new{Mapping=result.Plan.Mapping.Length,MaximumExportError=error});
                }
            }
        }
        finally
        {
            for(int i=0;i<sourceBones.Length;i++)sourceBones[i].Name=sourceNames[i];
            for(int i=0;i<targetBones.Length;i++)targetBones[i].Name=targetNames[i];
            for(int i=0;i<entries.Length;i++)entries[i].Name=trackNames[i];
            sourceModel.Name=sourceName;targetModel.Name=targetName;idle.Name=clipName;
        }
    }
    private static void CharacterizationCases(BrawlRig source,BrawlRig target,string output)
    {
        var selected=source.Rig.Roles.Where(p=>SkeletonCharacterizer.CoreRoles.Contains(p.Key)).OrderBy(p=>p.Value).ToArray();
        var indices=selected.Select((p,i)=>new{Original=p.Value,Index=i}).ToDictionary(p=>p.Original,p=>p.Index);
        var rootless=selected.Select(pair=>
        {
            int parent=source.Rig.Joints[pair.Value].Parent;
            while(parent>=0 && !indices.ContainsKey(parent))parent=source.Rig.Joints[parent].Parent;
            Matrix4x4 local=source.Rig.Rest.World[pair.Value];
            if(parent>=0){Matrix4x4.Invert(source.Rig.Rest.World[parent],out Matrix4x4 inverse);local*=inverse;}
            Matrix4x4.Decompose(local,out Vector3 scale,out Quaternion rotation,out Vector3 translation);
            return new RigJoint(GenericRoleName(pair.Key,"mixamo"),parent<0?-1:indices[parent],new JointTransform(translation,rotation,scale));
        }).ToArray();
        var rootlessMap=SkeletonCharacterizer.Analyze(rootless);SkeletonCharacterizer.Validate(rootless,rootlessMap.Roles);
        Check(rootlessMap.Roles.Count==17 && !rootlessMap.Roles.ContainsKey("root"),"A pelvis-root humanoid characterizes without global helpers, clavicles, toes or fingers");
        foreach(float unitScale in new[]{0.01f,100f})
        {
            var scaled=new Rig("Unrelated units","Humanoid",rootless.Select(j=>new RigJoint(j.Name,j.Parent,new JointTransform(j.Bind.Translation*unitScale,j.Bind.Rotation,j.Bind.Scale))),rootlessMap.Roles,source.Rig.HeadRadius*unitScale);
            var plan=new HumanoidRetargeter(scaled,target.Rig);var rest=plan.Evaluate(scaled.Rest).Pose;
            double error=Enumerable.Range(0,target.Bones.Length).Max(i=>Vector3.Distance(rest.Point(i),target.Rig.Rest.Point(i)));
            var animated=new RigPose(scaled);int arm=scaled.Roles["left.upperArm"];
            animated.SetWorldRotation(arm,Quaternion.CreateFromAxisAngle(Vector3.UnitZ,0.4f)*animated.Orientation[arm]);
            var posed=plan.Evaluate(animated).Pose;
            Check(error<0.005 && posed.World.All(m=>RigMath.Finite(m.Translation)),"Measured proportions handle unit scale "+unitScale+" without character-specific ratio limits",new{plan.LegScale,plan.ArmScale,plan.HeadScale,RestError=error});
        }
        foreach(Rig rig in new[]{source.Rig,target.Rig})
        {
            var auto=SkeletonCharacterizer.Analyze(rig.Joints);
            Check(auto.Missing.Length==0 && SkeletonCharacterizer.CoreRoles.All(r=>auto.Roles[r]==rig.Roles[r]),
                "Shared automatic characterization identifies the real anatomy of "+rig.Name,new{auto.Evidence,auto.Warnings});
            foreach(string convention in new[]{"mixamo","unreal","geometry"})
            {
                var renamed=rig.Joints.Select((j,i)=>new RigJoint("joint"+i,j.Parent,j.Bind)).ToArray();
                foreach(var pair in rig.Roles)
                {
                    string name=GenericRoleName(pair.Key,convention);
                    if(name!=null)renamed[pair.Value]=new RigJoint(name,renamed[pair.Value].Parent,renamed[pair.Value].Bind);
                }
                var inferred=SkeletonCharacterizer.Analyze(renamed);
                File.WriteAllText(Path.Combine(output,rig.Name+"-"+convention+"-mapping.json"),new System.Web.Script.Serialization.JavaScriptSerializer().Serialize(new{
                    Roles=inferred.Roles.ToDictionary(p=>p.Key,p=>new{Index=p.Value,Bone=renamed[p.Value].Name}),inferred.Warnings,inferred.Missing}));
                SkeletonCharacterizer.Validate(renamed,inferred.Roles);
                Check(inferred.Missing.Length==0 && SkeletonCharacterizer.CoreRoles.All(r=>inferred.Roles[r]==rig.Roles[r]),
                    convention+": characterize renamed "+rig.Name+" from its own names, hierarchy and rest geometry, without a supplied role map",inferred.Roles);
                if(convention=="geometry")Check(inferred.Warnings.Count>0,"Unnamed symmetric geometry requests orientation/mapping review");
                var resized=renamed.Select((j,i)=>new RigJoint(j.Name,j.Parent,new JointTransform(j.Bind.Translation*(rig.Roles.Any(p=>p.Value==i&&(p.Key.Contains("Leg")||p.Key.EndsWith(".foot")))?1.45f:
                    rig.Roles.Any(p=>p.Value==i&&(p.Key.Contains("Arm")||p.Key.EndsWith(".hand")))?0.7f:1),j.Bind.Rotation,j.Bind.Scale))).ToArray();
                var resizedMap=SkeletonCharacterizer.Analyze(resized);
                SkeletonCharacterizer.Validate(resized,resizedMap.Roles);
                Check(SkeletonCharacterizer.CoreRoles.All(r=>resizedMap.Roles[r]==rig.Roles[r]),convention+": characterization tolerates independent arm/leg proportion changes");
            }
        }
    }
    private static string GenericRoleName(string role,string convention)
    {
        if(convention=="geometry")return null;
        var center=new Dictionary<string,string>{{"root","Root"},{"pelvis","Hips"},{"spine","Spine"},{"chest","Spine2"},{"neck","Neck"},{"head","Head"}};
        if(center.TryGetValue(role,out string value))return convention=="mixamo"?"mixamorig:"+value:value.ToLowerInvariant();
        if(!role.Contains("."))return null;
        var names=new Dictionary<string,string>{{"shoulder","Shoulder"},{"upperArm","Arm"},{"lowerArm","ForeArm"},{"hand","Hand"},
            {"upperLeg","UpLeg"},{"lowerLeg","Leg"},{"foot","Foot"},{"toe","ToeBase"}};
        string[] parts=role.Split('.');if(!names.TryGetValue(parts[1],out value))return null;
        if(convention=="mixamo")return "mixamorig:"+(parts[0]=="left"?"Left":"Right")+value;
        var unreal=new Dictionary<string,string>{{"shoulder","clavicle"},{"upperArm","upperarm"},{"lowerArm","lowerarm"},{"hand","hand"},
            {"upperLeg","thigh"},{"lowerLeg","calf"},{"foot","foot"},{"toe","ball"}};
        return unreal[parts[1]]+"_"+(parts[0]=="left"?"l":"r");
    }
}
