using BrawlCrate.Studio.Retargeting;
using BrawlLib.SSBB.ResourceNodes;
using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Web.Script.Serialization;

internal static partial class Program
{
    // Read-only diagnosis of the solved reference, before any native fitting.
    static int FractionalRetargetProbe(string sourcePath,string targetPath,string motionPath,string name,string output,string timesText)
    {
        using(var s=NodeFactory.FromFile(null,sourcePath))using(var t=NodeFactory.FromFile(null,targetPath))using(var m=NodeFactory.FromFile(null,motionPath))
        {
            var source=BrawlRetargeter.Capture(Walk(s).OfType<MDL0Node>().First(n=>!n.Name.ToLowerInvariant().Contains("shadow")));
            var target=BrawlRetargeter.Capture(Walk(t).OfType<MDL0Node>().First(n=>!n.Name.ToLowerInvariant().Contains("shadow")));
            var clip=Walk(m).OfType<CHR0Node>().Single(c=>c.Name==name);
            var plan=new HumanoidRetargeter(source.Rig,target.Rig);
            var timer=System.Diagnostics.Stopwatch.StartNew();
            var contacts=new ContactMotion(plan,time=>BrawlRetargeter.Sample(source,clip,time),clip.Loop?clip.FrameCount:clip.FrameCount-1,true,requireFloorEvidence:true,loop:clip.Loop);
            long prepared=timer.ElapsedMilliseconds;
            object Pose(RigPose pose)=>pose.Local.Select((local,i)=>
            {
                var euler=BrawlRetargeter.Euler(local.Rotation);
                var reconstructed=BrawlRetargeter.Rotation(new BrawlLib.Internal.Vector3(euler.X,euler.Y,euler.Z));
                return new{Bone=target.Bones[i].Name,Index=i,Euler=euler,Quaternion=local.Rotation,Translation=local.Translation,World=pose.Orientation[i],Point=pose.Point(i),RoundtripError=SpanAngle(reconstructed,local.Rotation)};
            }).ToArray();
            var rows=timesText.Split(',').Select(text=>float.Parse(text,CultureInfo.InvariantCulture)).Select(time=>
            {
                long start=timer.ElapsedMilliseconds;var donor=BrawlRetargeter.Sample(source,clip,time);
                var fk=plan.Evaluate(donor,false);var ik=plan.Evaluate(donor);var corrected=contacts.Evaluate(time);
                return new{Time=time,Milliseconds=timer.ElapsedMilliseconds-start,Fk=Pose(fk.Pose),Ik=Pose(ik.Pose),Corrected=Pose(corrected.Pose),corrected.MaximumContactResidual,
                    ContactGoals=source.Rig.Surfaces.Keys.Select(role=>new{Role=role,Goal=contacts.At(role,time)}).ToArray()};
            }).ToArray();
            File.WriteAllText(output,new JavaScriptSerializer{MaxJsonLength=32*1024*1024,RecursionLimit=100}.Serialize(new{Source=source.Fingerprint,Target=target.Fingerprint,Clip=clip.Name,clip.FrameCount,PreparedMilliseconds=prepared,contacts.BodySolveIterations,Rows=rows}));
            Console.WriteLine("Read-only fractional probe: "+output);return 0;
        }
    }
}
