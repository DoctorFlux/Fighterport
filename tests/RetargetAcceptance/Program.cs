using BrawlLib.SSBB.ResourceNodes;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

internal static partial class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
        try
        {
            if(args.Length==4 && args[0]=="--motion-controls")return MotionControls(args[1],args[2],args[3]);
            if(args.Length==6 && args[0]=="--bake-probe")
            {
                if(System.IO.Directory.Exists(args[5]) && System.IO.Directory.EnumerateFileSystemEntries(args[5]).Any())throw new ArgumentException("Use a new or empty output directory.");
                System.IO.Directory.CreateDirectory(args[5]);
                using(var s=NodeFactory.FromFile(null,args[1]))using(var t=NodeFactory.FromFile(null,args[2]))using(var m=NodeFactory.FromFile(null,args[3]))
                {
                    var source=BrawlCrate.Studio.Retargeting.BrawlRetargeter.Capture(Walk(s).ToList().OfType<MDL0Node>().First(n=>!n.Name.ToLowerInvariant().Contains("shadow")));
                    var target=BrawlCrate.Studio.Retargeting.BrawlRetargeter.Capture(Walk(t).ToList().OfType<MDL0Node>().First(n=>!n.Name.ToLowerInvariant().Contains("shadow")));
                    var clip=Walk(m).ToList().OfType<CHR0Node>().Single(c=>c.Name==args[4]);
                    var timer=System.Diagnostics.Stopwatch.StartNew();int bucket=-1;
                    using(var result=BrawlCrate.Studio.Retargeting.BrawlRetargeter.Bake(source,target,clip,progress:(p,message)=>{
                        if(p/10==bucket)return;bucket=p/10;Console.WriteLine(timer.ElapsedMilliseconds+" ms "+p+"% "+message);
                    }))
                    {
                        long bakeMilliseconds=timer.ElapsedMilliseconds;
                        string path=System.IO.Path.Combine(args[5],"output.chr0");result.Animation.ExportChecked(path);
                        using(var saved=(CHR0Node)NodeFactory.FromFile(null,path))
                            if(saved.FrameCount!=clip.FrameCount || saved.Loop!=clip.Loop)throw new System.IO.InvalidDataException("Native output metadata differs.");
                        string json=new System.Web.Script.Serialization.JavaScriptSerializer().Serialize(new{Clip=clip.Name,clip.FrameCount,BakeMilliseconds=bakeMilliseconds,
                            result.Contacts.BodySolveIterations,result.Contacts.PlantedBodySamples,result.MaximumContactResidual,result.MaximumLimbResidual});
                        System.IO.File.WriteAllText(System.IO.Path.Combine(args[5],"bake-probe.json"),json);Console.WriteLine(json);
                    }
                }
                return 0;
            }
            if(args.Length==5 && args[0]=="--stance-probe")
            {
                using(var root=NodeFactory.FromFile(null,args[1]))using(var motion=NodeFactory.FromFile(null,args[2]))
                {
                    var model=Walk(root).ToList().OfType<MDL0Node>().First(n=>!n.Name.ToLowerInvariant().Contains("shadow"));
                    var rig=BrawlCrate.Studio.Retargeting.BrawlRetargeter.Capture(model);
                    var clip=Walk(motion).ToList().OfType<CHR0Node>().Single(c=>c.Name==args[3]);
                    var records=new List<object>();
                    for(int i=0;i<clip.FrameCount;i++)
                    {
                        model.ApplyCHR(clip,i+1);var pose=BrawlCrate.Studio.Retargeting.BrawlRetargeter.Sample(rig,clip,i);
                        records.Add(new{Time=i,Hip=pose.Point("pelvis"),MeshMinimum=Vertices(model).Min(p=>p.Y),Feet=new[]{"left","right"}.Select(side=>new{
                            Side=side,Minimum=rig.Rig.Surfaces[side+".foot"].Clearance(pose),Ankle=pose.Point(side+".foot")
                        }).ToArray()});
                    }
                    System.IO.File.WriteAllText(args[4],new System.Web.Script.Serialization.JavaScriptSerializer{MaxJsonLength=16000000}.Serialize(new{rig.Rig.GroundHeight,Records=records}));
                }
                return 0;
            }
            if(args.Length==3 && args[0]=="--characterize")
            {
                using(var root=NodeFactory.FromFile(null,args[1]))
                {
                    var model=Walk(root).ToList().OfType<MDL0Node>().First(n=>!n.Name.ToLowerInvariant().Contains("shadow"));
                    var map=BrawlCrate.Studio.Retargeting.BrawlRetargeter.Characterize(model);
                    var rig=BrawlCrate.Studio.Retargeting.BrawlRetargeter.Capture(model);
                    System.IO.File.WriteAllText(args[2],new System.Web.Script.Serialization.JavaScriptSerializer().Serialize(new{model.Name,Roles=map.Roles.ToDictionary(p=>p.Key,p=>rig.Bones[p.Value].Name),map.Evidence,map.Warnings,map.Missing}));
                }
                return 0;
            }
            if(args.Length==8 && args[0]=="--pose-probe")return ProbePoses(args[1],args[2],args[3],args[4],args[5],args[6]=="-"?null:args[6],float.Parse(args[7],CultureInfo.InvariantCulture));
            if(args.Length==6 && args[0]=="--pose-probe")return ProbePoses(args[1],args[2],args[3],args[4],args[5]);
            if(args.Length==7 && args[0]=="--pose-probe")return ProbePoses(args[1],args[2],args[3],args[4],args[5],args[6]);
            if (args.Length == 4 && args[0] == "--contacts") return ProbeContacts(args[1], args[2], args[3]);
            if (args.Length == 2 && args[0] == "--dump")
            {
                using (ResourceNode root = NodeFactory.FromFile(null, args[1]))
                {
                    MDL0Node model = Walk(root).ToList().OfType<MDL0Node>().First(m => m.Name.IndexOf("shadow", StringComparison.OrdinalIgnoreCase) < 0);
                    foreach (MDL0BoneNode b in model.AllBones)
                        Console.WriteLine($"{b.Name,-18} parent={b.Parent.Name,-18} localT={b.BindState.Translate} localR={b.BindState.Rotate} localS={b.BindState.Scale} world={b.BindMatrix.GetPoint()}");
                }
                return 0;
            }
            return Run(args);
        }
        catch (Exception error) { Console.Error.WriteLine(error); return 1; }
    }

    private static int ProbeContacts(string modelPath, string motionPath, string output)
    {
        using (ResourceNode root=NodeFactory.FromFile(null,modelPath))
        using (ResourceNode motions=NodeFactory.FromFile(null,motionPath))
        {
            var model=Walk(root).ToList().OfType<MDL0Node>().First(m=>m.Name.IndexOf("shadow",StringComparison.OrdinalIgnoreCase)<0);
            var rig=BrawlCrate.Studio.Retargeting.BrawlRetargeter.Capture(model);
            var records=new List<object>();
            foreach(var clip in Walk(motions).ToList().OfType<CHR0Node>().Where(c=>c.Children.Any(b=>b.Name=="HipN")&&c.Children.Any(b=>b.Name=="BustN")))
            {
                float closest=float.MaxValue,bodyClosest=float.MaxValue; int frame=0,bodyFrame=0; string role=null,bodyRole=null;
                for(int i=0;i<clip.FrameCount;i+=2)
                {
                    var pose=BrawlCrate.Studio.Retargeting.BrawlRetargeter.Sample(rig,clip,i);
                    foreach(string hand in new[]{"left.hand","right.hand"})
                    { float h=rig.Rig.Surfaces[hand].Sample(pose).Clearance; if(h<closest){closest=h;frame=i;role=hand;} }
                    foreach(string body in rig.Rig.Surfaces.Keys.Where(r=>!r.EndsWith(".hand")&&!r.EndsWith(".foot")))
                    {float h=rig.Rig.Surfaces[body].Clearance(pose);if(h<bodyClosest){bodyClosest=h;bodyFrame=i;bodyRole=body;}}
                }
                records.Add(new {Clip=clip.Name,MinimumHandClearance=closest,Frame=frame,Role=role,MinimumBodyClearance=bodyClosest,BodyFrame=bodyFrame,BodyRole=bodyRole});
            }
            var serializer=new System.Web.Script.Serialization.JavaScriptSerializer{MaxJsonLength=16000000};
            System.IO.File.WriteAllText(output,serializer.Serialize(new {rig.Rig.GroundHeight,Surfaces=rig.Rig.Surfaces.Select(p=>new {Role=p.Key,Points=p.Value.Points.Length,Rest=p.Value.Sample(rig.Rig.Rest)}).ToArray(),Clips=records}));
            Console.WriteLine(output); return 0;
        }
    }

    private static int ProbePoses(string sourcePath,string targetPath,string motionsPath,string name,string output,string nativePath=null,float? probeTime=null)
    {
        using(var s=NodeFactory.FromFile(null,sourcePath))using(var t=NodeFactory.FromFile(null,targetPath))using(var m=NodeFactory.FromFile(null,motionsPath))
        using(var native=nativePath==null?null:NodeFactory.FromFile(null,nativePath))
        {
            var source=BrawlCrate.Studio.Retargeting.BrawlRetargeter.Capture(Walk(s).ToList().OfType<MDL0Node>().First(n=>!n.Name.ToLowerInvariant().Contains("shadow")));
            var target=BrawlCrate.Studio.Retargeting.BrawlRetargeter.Capture(Walk(t).ToList().OfType<MDL0Node>().First(n=>!n.Name.ToLowerInvariant().Contains("shadow")));
            var clip=Walk(m).ToList().OfType<CHR0Node>().Single(c=>c.Name==name);
            var plan=new BrawlCrate.Studio.Retargeting.HumanoidRetargeter(source.Rig,target.Rig);
            var motion=new BrawlCrate.Studio.Retargeting.ContactMotion(plan,time=>BrawlCrate.Studio.Retargeting.BrawlRetargeter.Sample(source,clip,time),clip.FrameCount-1,true,requireFloorEvidence:true,loop:clip.Loop);
            long calibrationIterations=motion.BodySolveIterations;
            var records=new List<object>();var timer=System.Diagnostics.Stopwatch.StartNew();
            foreach(float time in probeTime.HasValue?new[]{probeTime.Value}:Enumerable.Range(0,(clip.FrameCount-1)*4+1).Select(i=>i/4f))
            {
                var pose=motion.Evaluate(time);
                var sourcePose=BrawlCrate.Studio.Retargeting.BrawlRetargeter.Sample(source,clip,time);
                var actual=native is CHR0Node saved?BrawlCrate.Studio.Retargeting.BrawlRetargeter.Sample(target,saved,time):pose.Pose;
                var baseline=plan.Evaluate(sourcePose).Pose;
                records.Add(new{Time=time,pose.MaximumContactResidual,pose.PelvisCorrection,Contacts=target.Rig.Surfaces.Select(p=>new{Role=p.Key,Support=motion.At(p.Key,time).Support,Lock=motion.At(p.Key,time).Lock,SourceHeight=source.Rig.Surfaces[p.Key].Clearance(sourcePose),SourceFlatness=source.Rig.Surfaces[p.Key].Sample(sourcePose).Flatness,Flatness=p.Value.Sample(pose.Pose).Flatness,Height=p.Value.Clearance(pose.Pose),NativeHeight=p.Value.Clearance(actual),PlanarError=PlanarContactError(motion.At(p.Key,time),pose.Pose)}).ToArray(),
                    Grips=new[]{"left","right"}.Select(side=>new{Side=side,Weight=motion.GripWeight(side,time),SolverError=motion.Suspended==null?0:System.Numerics.Vector3.Distance(pose.Pose.Point(side+".hand"),motion.Suspended.GripGoal(sourcePose,side)),NativeError=motion.Suspended==null?0:System.Numerics.Vector3.Distance(actual.Point(side+".hand"),motion.Suspended.GripGoal(sourcePose,side))}).ToArray(),
                    Limbs=new[]{"left.hand","right.hand","left.foot","right.foot"}.Select(role=>{int j=target.Rig.Roles[role],parent=target.Rig.Joints[j].Parent;return new{Role=role,WorldLength=System.Numerics.Vector3.Distance(pose.Pose.Point(j),pose.Pose.Point(parent)),BaselineLength=System.Numerics.Vector3.Distance(baseline.Point(j),baseline.Point(parent)),Local=pose.Pose.Local[j].Translation,BaseLocal=baseline.Local[j].Translation};}).ToArray(),
                    Scales=target.Bones.Select((b,i)=>new{b.Name,pose.Pose.Local[i].Scale}).Where(b=>Math.Abs(b.Scale.X-b.Scale.Y)>0.0001f || Math.Abs(b.Scale.X-b.Scale.Z)>0.0001f).ToArray(),
                    Bones=probeTime.HasValue?target.Bones.Select((b,i)=>new{b.Name,Error=System.Numerics.Vector3.Distance(pose.Pose.Point(i),actual.Point(i)),Expected=pose.Pose.Point(i),Actual=actual.Point(i),ExpectedEuler=BrawlCrate.Studio.Retargeting.BrawlRetargeter.Euler(pose.Pose.Local[i].Rotation),ActualEuler=BrawlCrate.Studio.Retargeting.BrawlRetargeter.Euler(actual.Local[i].Rotation)}).ToArray():null});
            }
            var serializer=new System.Web.Script.Serialization.JavaScriptSerializer{MaxJsonLength=16000000};
            var json=serializer.Serialize(new{Clip=name,motion.FloorEnabled,Milliseconds=timer.ElapsedMilliseconds,BodyIterations=motion.BodySolveIterations-calibrationIterations,CalibrationIterations=calibrationIterations,Records=records});
            System.IO.File.WriteAllText(output,json);Console.WriteLine(json);return 0;
        }
    }

    private static float PlanarContactError(BrawlCrate.Studio.Retargeting.ContactGoal goal,BrawlCrate.Studio.Retargeting.RigPose pose)
    {
        if(goal.Marker==null)return 0;
        var delta=goal.Marker.Evaluate(pose)-goal.Anchor;
        delta-=pose.Rig.GroundNormal*System.Numerics.Vector3.Dot(delta,pose.Rig.GroundNormal);
        return delta.Length();
    }

    private static IEnumerable<ResourceNode> Walk(ResourceNode node)
    {
        yield return node;
        foreach (ResourceNode child in node.Children)
            foreach (ResourceNode descendant in Walk(child)) yield return descendant;
    }
}
