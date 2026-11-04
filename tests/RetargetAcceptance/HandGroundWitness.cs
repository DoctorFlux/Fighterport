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
    // Diagnostic only: inspect the existing native result and the exact source
    // support proposal. This does not tune, apply, or approve a contact.
    private static int HandGroundWitness(string sourcePath,string targetPath,string motionPath,
        string clipName,string nativeOutputPath,string reportPath)
    {
        using(var sourceRoot=NodeFactory.FromFile(null,sourcePath))
        using(var targetRoot=NodeFactory.FromFile(null,targetPath))
        using(var motionRoot=NodeFactory.FromFile(null,motionPath))
        using(var nativeRoot=NodeFactory.FromFile(null,nativeOutputPath))
        {
            var sourceModel=Walk(sourceRoot).OfType<MDL0Node>().First(m=>
                m.Name.IndexOf("shadow",StringComparison.OrdinalIgnoreCase)<0);
            var targetModel=Walk(targetRoot).OfType<MDL0Node>().First(m=>
                m.Name.IndexOf("shadow",StringComparison.OrdinalIgnoreCase)<0);
            var clip=Walk(motionRoot).OfType<CHR0Node>().Single(c=>c.Name==clipName);
            var saved=Walk(nativeRoot).OfType<CHR0Node>().Single();
            if(saved.Name!=clip.Name||saved.FrameCount!=clip.FrameCount||saved.Loop!=clip.Loop)
                throw new InvalidDataException("The native result does not match the selected source clip timing.");
            var source=BrawlRetargeter.Capture(sourceModel);
            var target=BrawlRetargeter.Capture(targetModel);
            using(var prepared=BrawlRetargeter.Bake(source,target,clip))
            {
                var times=Enumerable.Range(0,(clip.FrameCount-1)*8+1).Select(i=>i/8f).ToArray();
                var rows=times.Select(time=>
                {
                    var sourcePose=BrawlRetargeter.Sample(source,clip,time);
                    var nativePose=BrawlRetargeter.Sample(target,saved,time);
                    return new {Time=time,Hands=new[]{"left.hand","right.hand"}.Select(role=>
                    {
                        var sourceSurface=source.Rig.Surfaces[role];
                        var targetSurface=target.Rig.Surfaces[role];
                        var sourceSample=sourceSurface.Sample(sourcePose);
                        var nativeSample=targetSurface.Sample(nativePose);
                        var goal=prepared.Contacts.At(role,time);
                        var before=BrawlRetargeter.Sample(source,clip,Math.Max(0,time-.25f));
                        var after=BrawlRetargeter.Sample(source,clip,Math.Min(clip.FrameCount-1,time+.25f));
                        var marker=sourceSurface.Marker(sourcePose);
                        Vector3 velocity=(marker.Evaluate(after)-marker.Evaluate(before)) /
                            Math.Max(.25f,Math.Min(clip.FrameCount-1,time+.25f)-Math.Max(0,time-.25f));
                        float vertical=Math.Abs(Vector3.Dot(velocity,source.Rig.GroundNormal));
                        float planar=(velocity-source.Rig.GroundNormal*Vector3.Dot(velocity,source.Rig.GroundNormal)).Length();
                        float length=Vector3.Distance(source.Rig.Rest.Point(role.Replace("hand","upperArm")),source.Rig.Rest.Point(role.Replace("hand","lowerArm")))+
                            Vector3.Distance(source.Rig.Rest.Point(role.Replace("hand","lowerArm")),source.Rig.Rest.Point(role));
                        Vector3 drift=goal.Marker==null?Vector3.Zero:goal.Marker.Evaluate(nativePose)-goal.Anchor;
                        drift-=target.Rig.GroundNormal*Vector3.Dot(drift,target.Rig.GroundNormal);
                        return new {Role=role,SourceClearance=sourceSample.Clearance,SourceClearancePerArm=sourceSample.Clearance/length,
                            SourceFlatness=sourceSample.Flatness,SourceVerticalSpeedPerArm=vertical/length,
                            SourcePlanarSpeedPerArm=planar/length,Support=goal.Support,Lock=goal.Lock,
                            NativeTargetClearance=nativeSample.Clearance,NativeTargetFlatness=nativeSample.Flatness,
                            NativePlanarLockDrift=goal.Marker==null?(float?)null:drift.Length()};
                    }).ToArray()};
                }).ToArray();
                var summary=new[]{"left.hand","right.hand"}.Select(role=>
                {
                    var quarter=rows.Where(r=>r.Time*4==(int)(r.Time*4)).Select(r=>r.Hands.Single(h=>h.Role==role)).ToArray();
                    int full=quarter.Count(h=>h.Support>.99f),partial=quarter.Count(h=>h.Support>.5f);
                    int run=0,longest=0;
                    foreach(var hand in quarter){run=hand.Support>.5f?run+1:0;longest=Math.Max(longest,run);}
                    string side=role.Split('.')[0];
                    float targetArmLength=Vector3.Distance(target.Rig.Rest.Point(side+".upperArm"),target.Rig.Rest.Point(side+".lowerArm"))+
                        Vector3.Distance(target.Rig.Rest.Point(side+".lowerArm"),target.Rig.Rest.Point(role));
                    var withheld=rows.Where(r=>r.Time*4!=(int)(r.Time*4)).Select(r=>new{r.Time,Hand=r.Hands.Single(h=>h.Role==role)})
                        .Where(r=>r.Hand.Support>.99f).OrderByDescending(r=>Math.Abs(r.Hand.NativeTargetClearance)).FirstOrDefault();
                    float? solvedAtWorst=withheld==null?(float?)null:
                        target.Rig.Surfaces[role].Clearance(prepared.Contacts.Evaluate(withheld.Time).Pose);
                    float? independentNativeAtWorst=null;
                    if(withheld!=null)
                    {
                        targetModel.ApplyCHR(saved,withheld.Time+1);
                        var member=targetModel.PolygonList.Cast<MDL0ObjectNode>().SelectMany(obj=>obj.Vertices)
                            .Where(vertex=>NativeSurfaceMember(vertex,target,role,target.Bones[target.Rig.Roles[role]])).ToArray();
                        if(member.Length==0)throw new InvalidDataException("The independent native hand skin patch is empty.");
                        independentNativeAtWorst=member.Min(vertex=>Vector3.Dot(BrawlRetargeter.Vector(vertex.WeightedPosition),target.Rig.GroundNormal))-
                            target.Rig.GroundHeight;
                    }
                    return new{Role=role,FullQuarterSamples=full,PartialQuarterSamples=partial,
                        LongestPartialQuarterRun=longest,MaxLock=quarter.Max(h=>h.Lock),TargetArmLength=targetArmLength,
                        TargetHandPositionBound=targetArmLength*.001f,
                        NativeFullContactClearance=quarter.Where(h=>h.Support>.99f).Select(h=>Math.Abs(h.NativeTargetClearance)).DefaultIfEmpty(0).Max(),
                        NativeWithheldContactClearance=rows.Where(r=>r.Time*4!=(int)(r.Time*4)).Select(r=>r.Hands.Single(h=>h.Role==role))
                            .Where(h=>h.Support>.99f).Select(h=>Math.Abs(h.NativeTargetClearance)).DefaultIfEmpty(0).Max(),
                        WorstWithheldTime=withheld==null?(float?)null:withheld.Time,
                        WorstWithheldNativeClearance=withheld==null?(float?)null:withheld.Hand.NativeTargetClearance,
                        WorstWithheldSolvedClearance=solvedAtWorst,
                        WorstWithheldIndependentNativeClearance=independentNativeAtWorst};
                }).ToArray();
                var report=new{SourceModel=sourceModel.Name,TargetModel=targetModel.Name,clip.Name,clip.FrameCount,clip.Loop,
                    SourceModelSha256=FighterProject.HashFile(sourcePath),TargetModelSha256=FighterProject.HashFile(targetPath),
                    SourceMotionSha256=FighterProject.HashFile(motionPath),NativeOutputSha256=FighterProject.HashFile(nativeOutputPath),
                    prepared.Contacts.FloorEnabled,prepared.Contacts.PlantedHandSamples,
                    SupportRefinementUnavailable=prepared.OptimizationReport?.Contains("Support refinement unavailable")??false,
                    Summary=summary,Rows=rows,
                    Coverage="All integer, quarter, and withheld eighth frames; support is sampled from the source-derived proposal. No continuous-time or game-floor claim."};
                Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(reportPath)));
                File.WriteAllText(reportPath,new JavaScriptSerializer{MaxJsonLength=16000000}.Serialize(report));
                Console.WriteLine("Hand witness: "+clip.Name+" "+sourceModel.Name+" -> "+targetModel.Name+
                    "; quarter hand support "+string.Join(", ",summary.Select(s=>s.Role+" "+s.FullQuarterSamples+" full / "+s.PartialQuarterSamples+" partial")));
            }
        }
        return 0;
    }
}
