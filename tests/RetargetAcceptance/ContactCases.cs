using BrawlCrate.Studio.Retargeting;
using BrawlLib.SSBB.ResourceNodes;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;

internal static partial class Program
{
    private static void ContactCases(BrawlRig source,BrawlRig target,Dictionary<string,CHR0Node> clips,string output,MDL0Node nativeTarget)
    {
        var measures=new List<object>();
        var nativeSurfaces=target.Rig.Surfaces.Keys.ToDictionary(role=>role,role=>nativeTarget.PolygonList.Cast<MDL0ObjectNode>().SelectMany(o=>o.Vertices)
            .Where(v=>NativeSurfaceMember(v,target,role,target.Bones[target.Rig.Roles[role]])).ToArray());
        bool contactsPass=true;
        foreach(string name in new[]{"Wait1","Wait2","Wait3","Attack11","Run","LandingAirN","AttackLw4","DownStandU","JumpSquat","JumpF","Fall","AttackAirF","DownWaitU","DownWaitD","SquatWait","SlipWait","CliffClimbQuick","CliffClimbSlow","CliffAttackQuick","CliffAttackSlow"}.Where(n=>(ContactClip==null || n==ContactClip) && (!FocusedSupportCases || ReviewContactNames.Contains(n))))
        {
            CHR0Node clip=clips[name];
            var bakeTimer=System.Diagnostics.Stopwatch.StartNew();
            using(var result=BrawlRetargeter.Bake(source,target,clip,groundMode:name=="AttackAirF"?GroundContactMode.Airborne:GroundContactMode.Auto))
            {
                long bakeMilliseconds=bakeTimer.ElapsedMilliseconds;
                if(name=="SlipWait" || name=="DownWaitD")
                {
                    int last=clip.Loop?clip.FrameCount:clip.FrameCount-1;
                    var fresh=new ContactMotion(result.Plan,t=>BrawlRetargeter.Sample(source,clip,t),last,true,requireFloorEvidence:true);
                    float time=(clip.FrameCount-1)*0.37f;
                    var expected=result.Contacts.Evaluate(time).Pose.World;
                    fresh.Evaluate(last);fresh.Evaluate(0);
                    var actual=fresh.Evaluate(time).Pose.World;
                    Check(expected.SequenceEqual(actual),name+": fresh body contacts are independent of seek order and cached bake poses");
                }
                string path=Path.Combine(output,name+"-contact.chr0"); result.Animation.ExportChecked(path);
                using(var saved=(CHR0Node)NodeFactory.FromFile(null,path))
                {
                    Check(saved.FrameCount==clip.FrameCount && saved.Loop==clip.Loop,name+": native contact export preserves timing and loop metadata");
                    double nativeGripError=0,solverGripError=0;
                    double trajectoryError=0,footSkinAgreement=0,rawTravelZError=0,airFkError=0,airNativeIntegerError=0,maxAirContact=0;
                    var timeline=new List<object>();
                    double allFootClearance=0,foot=0,hand=0,body=0,before=0,after=0,lockError=0,integerLockError=0,solvedLockError=0,limbError=0,solvedError=0,integerError=0,bodySolved=0,bodyInteger=0,endSolved=0,endInteger=0,nearFootExcess=0,nearFootIntegerExcess=0,nearFootSolverExcess=0,kneePoleDot=1,endIntegerLock=0,bodyIntegerLock=0; int feet=0,hands=0,bodies=0,locks=0; float pelvis=0,worstFrame=0,bodyWorstTime=0;string bodyWorstRole=null;
                    for(int i=0;i<(clip.FrameCount-1)*4+1;i++)
                    {
                        float time=i/4f;
                        RigPose sourcePose=BrawlRetargeter.Sample(source,clip,time), baseline=result.Plan.Evaluate(sourcePose).Pose;
                        RetargetPose solved=result.Contacts.Evaluate(time); RigPose actual=BrawlRetargeter.Sample(target,saved,time);
                        nativeTarget.ApplyCHR(saved,time+1);
                        var trajectory=result.Plan.Mapping.Where(result.Plan.IsTrajectory).Select(map=>{
                            Vector3 expected=target.Rig.Joints[map.TargetIndex].Bind.Translation+result.Plan.TransferTranslationDelta(map,
                                sourcePose.Local[map.SourceIndex].Translation-source.Rig.Joints[map.SourceIndex].Bind.Translation);
                            trajectoryError=Math.Max(trajectoryError,Vector3.Distance(expected,actual.Local[map.TargetIndex].Translation));
                            trajectoryError=Math.Max(trajectoryError,Vector3.Distance(expected,solved.Pose.Local[map.TargetIndex].Translation));
                            rawTravelZError=Math.Max(rawTravelZError,Math.Abs(sourcePose.Local[map.SourceIndex].Translation.Z-actual.Local[map.TargetIndex].Translation.Z));
                            return new{SourceBone=map.Source,TargetBone=map.Target,Source=sourcePose.Local[map.SourceIndex].Translation,Expected=expected,Native=actual.Local[map.TargetIndex].Translation};
                        }).ToArray();
                        var feetTimeline=new[]{"left","right"}.Select(side=>{
                            string role=side+".foot";
                            float nativeHeight=nativeSurfaces[role].Min(v=>v.WeightedPosition._y)-target.Rig.GroundHeight;
                            footSkinAgreement=Math.Max(footSkinAgreement,Math.Abs(nativeHeight-target.Rig.Surfaces[role].Clearance(actual)));
                            return new{Side=side,Support=result.Contacts.At(role,time).Support,Lock=result.Contacts.At(role,time).Lock,SourceHeight=source.Rig.Surfaces[role].Clearance(sourcePose),
                                SolvedHeight=target.Rig.Surfaces[role].Clearance(solved.Pose),NativeHeight=nativeHeight,SourceAnkle=sourcePose.Point(role),NativeAnkle=actual.Point(role)};
                        }).ToArray();
                        timeline.Add(new{Time=time,Feet=feetTimeline,Trajectory=trajectory});
                        if(name=="JumpF" || name=="Fall")
                        {
                            foreach(string role in new[]{"left.foot","right.foot"})maxAirContact=Math.Max(maxAirContact,Math.Max(result.Contacts.At(role,time).Support,result.Contacts.At(role,time).Lock));
                            airFkError=Math.Max(airFkError,Enumerable.Range(0,target.Bones.Length).Max(j=>Vector3.Distance(baseline.Point(j),solved.Pose.Point(j))));
                            if(time==(int)time)airNativeIntegerError=Math.Max(airNativeIntegerError,Enumerable.Range(0,target.Bones.Length).Max(j=>Vector3.Distance(baseline.Point(j),actual.Point(j))));
                        }
                        if(result.Contacts.Suspended!=null)foreach(string side in new[]{"left","right"})
                        {
                            if(result.Contacts.Suspended.GripWeight(sourcePose,side)<0.99f)continue;
                            Vector3 goal=result.Contacts.Suspended.GripGoal(sourcePose,side);
                            solverGripError=Math.Max(solverGripError,Vector3.Distance(solved.Pose.Point(side+".hand"),goal));
                            nativeGripError=Math.Max(nativeGripError,Vector3.Distance(actual.Point(side+".hand"),goal));
                        }
                        pelvis=Math.Max(pelvis,solved.PelvisCorrection.Length());
                        foreach(string side in new[]{"left","right"})allFootClearance=Math.Max(allFootClearance,Math.Abs(target.Rig.Surfaces[side+".foot"].Clearance(actual)));
                        foreach(string role in target.Rig.Surfaces.Keys)
                        {
                            ContactGoal contact=result.Contacts.At(role,time);
                            if(contact.Support<0.99f)continue;
                            double h=Math.Abs(target.Rig.Surfaces[role].Sample(actual).Clearance);
                            solvedError=Math.Max(solvedError,Math.Abs(target.Rig.Surfaces[role].Sample(solved.Pose).Clearance));
                            bool endContact=role.EndsWith(".hand")||role.EndsWith(".foot");
                            double solverHeight=Math.Abs(target.Rig.Surfaces[role].Clearance(solved.Pose));
                            if(endContact){endSolved=Math.Max(endSolved,solverHeight);if(time==(int)time)endInteger=Math.Max(endInteger,h);}
                            else{if(solverHeight>bodySolved){bodyWorstTime=time;bodyWorstRole=role;}bodySolved=Math.Max(bodySolved,solverHeight);if(time==(int)time)bodyInteger=Math.Max(bodyInteger,h);}
                            if(time==(int)time)integerError=Math.Max(integerError,h);
                            if(h>Math.Max(body,Math.Max(foot,hand)))worstFrame=time;
                            if(role.EndsWith(".hand")){hand=Math.Max(hand,h);hands++;}
                            else if(role.EndsWith(".foot")){foot=Math.Max(foot,h);feet++;}
                            else{body=Math.Max(body,h);bodies++;}
                            before+=Math.Abs(target.Rig.Surfaces[role].Sample(baseline).Clearance);
                            after+=h;
                            if(contact.Lock>0.99f)
                            {
                                Vector3 delta=contact.Marker.Evaluate(actual)-contact.Anchor;
                                delta-=target.Rig.GroundNormal*Vector3.Dot(delta,target.Rig.GroundNormal);
                                lockError=Math.Max(lockError,delta.Length());locks++;
                                if(time==(int)time)integerLockError=Math.Max(integerLockError,delta.Length());
                                if(time==(int)time){if(endContact)endIntegerLock=Math.Max(endIntegerLock,delta.Length());else bodyIntegerLock=Math.Max(bodyIntegerLock,delta.Length());}
                                Vector3 solvedDelta=contact.Marker.Evaluate(solved.Pose)-contact.Anchor;
                                solvedDelta-=target.Rig.GroundNormal*Vector3.Dot(solvedDelta,target.Rig.GroundNormal);
                                solvedLockError=Math.Max(solvedLockError,solvedDelta.Length());
                            }
                        }
                        foreach(string side in new[]{"left","right"})foreach(string end in new[]{"hand","foot"})
                        {
                            int joint=target.Rig.Roles[side+"."+end]; int parent=target.Rig.Joints[joint].Parent;
                            limbError=Math.Max(limbError,Vector3.Distance(solved.Pose.Local[joint].Translation,baseline.Local[joint].Translation));
                            // Rotation below authored anisotropic scale changes
                            // world length without changing the bone offset.
                            bool anisotropic=false;
                            for(int ancestor=parent;ancestor>=0;ancestor=target.Rig.Joints[ancestor].Parent)
                            {
                                Vector3 scale=baseline.Local[ancestor].Scale;
                                anisotropic|=Math.Abs(scale.X-scale.Y)>0.0001f || Math.Abs(scale.X-scale.Z)>0.0001f;
                            }
                            if(!anisotropic)limbError=Math.Max(limbError,Math.Abs(Vector3.Distance(solved.Pose.Point(joint),solved.Pose.Point(parent))-Vector3.Distance(baseline.Point(joint),baseline.Point(parent))));
                        }
                        limbError=Math.Max(limbError,Enumerable.Range(0,target.Bones.Length).Max(b=>Vector3.Distance(solved.Pose.Local[b].Scale,baseline.Local[b].Scale)));
                        foreach(string side in new[]{"left","right"})
                        {
                            float sourceLength=Vector3.Distance(source.Rig.Rest.Point(side+".upperLeg"),source.Rig.Rest.Point(side+".lowerLeg"))+Vector3.Distance(source.Rig.Rest.Point(side+".lowerLeg"),source.Rig.Rest.Point(side+".foot"));
                            float targetLength=Vector3.Distance(target.Rig.Rest.Point(side+".upperLeg"),target.Rig.Rest.Point(side+".lowerLeg"))+Vector3.Distance(target.Rig.Rest.Point(side+".lowerLeg"),target.Rig.Rest.Point(side+".foot"));
                            float sourceHeight=source.Rig.Surfaces[side+".foot"].Clearance(sourcePose);
                            if(result.Contacts.FloorEnabled && result.Contacts.NearFloorFeet(time)[side+".foot"].Item1>0.99f && sourceHeight>=0 && sourceHeight<sourceLength*0.1f)
                            {
                                nearFootExcess=Math.Max(nearFootExcess,target.Rig.Surfaces[side+".foot"].Clearance(actual)-sourceHeight*targetLength/sourceLength);
                                nearFootSolverExcess=Math.Max(nearFootSolverExcess,target.Rig.Surfaces[side+".foot"].Clearance(solved.Pose)-sourceHeight*targetLength/sourceLength);
                                if(time==(int)time)nearFootIntegerExcess=Math.Max(nearFootIntegerExcess,target.Rig.Surfaces[side+".foot"].Clearance(actual)-sourceHeight*targetLength/sourceLength);
                            }
                            if(result.Contacts.At(side+".foot",time).Support<0.99f || target.Rig.Surfaces.Keys.Any(r=>!r.EndsWith(".foot")&&!r.EndsWith(".hand")&&result.Contacts.At(r,time).Support>0))continue;
                            Vector3 sourceBend=KneeBend(sourcePose,side),targetBend=KneeBend(solved.Pose,side);
                            Vector3 axis=Vector3.Normalize(solved.Pose.Point(side+".foot")-solved.Pose.Point(side+".upperLeg"));
                            Vector3 pole=Vector3.Transform(sourceBend,result.Plan.Calibration);pole-=axis*Vector3.Dot(axis,pole);
                            if(pole.Length()>sourceLength*0.01f && targetBend.Length()>targetLength*0.01f)
                                kneePoleDot=Math.Min(kneePoleDot,Vector3.Dot(Vector3.Normalize(pole),Vector3.Normalize(targetBend)));
                        }
                    }
                    var measurement=new {Clip=name,BakeMilliseconds=bakeMilliseconds,TrajectoryError=trajectoryError,FootSkinAgreement=footSkinAgreement,EndSourceFeet=new[]{"left","right"}.ToDictionary(side=>side,side=>source.Rig.Surfaces[side+".foot"].Clearance(BrawlRetargeter.Sample(source,clip,clip.FrameCount-1))),EndTargetFeet=new[]{"left","right"}.ToDictionary(side=>side,side=>target.Rig.Surfaces[side+".foot"].Clearance(BrawlRetargeter.Sample(target,saved,clip.FrameCount-1))),SuspendedSupportDetected=result.Contacts.Suspended!=null,NativeGripError=nativeGripError,SolverGripError=solverGripError,BodyIterations=result.Contacts.BodySolveIterations,AllFootClearance=allFootClearance,StabilizedFeet=result.Contacts.StabilizedStandingFeet,FootSamples=feet,HandSamples=hands,BodySamples=bodies,LockedSamples=locks,FootClearance=foot,HandClearance=hand,BodyClearance=body,SolvedClearance=solvedError,IntegerClearance=integerError,BodySolved=bodySolved,BodyInteger=bodyInteger,BodyWorstTime=bodyWorstTime,BodyWorstRole=bodyWorstRole,EndSolved=endSolved,EndInteger=endInteger,WorstFrame=worstFrame,LockError=lockError,IntegerLockError=integerLockError,EndIntegerLock=endIntegerLock,BodyIntegerLock=bodyIntegerLock,SolvedLockError=solvedLockError,Before=before,After=after,MaximumPelvisShift=pelvis,LimbLengthError=limbError,NearFootHeightExcess=nearFootExcess,NearFootIntegerExcess=nearFootIntegerExcess,NearFootSolverExcess=nearFootSolverExcess,MinimumKneePoleDot=kneePoleDot};
                    File.WriteAllText(Path.Combine(output,name+"-feet-trajectory.json"),new System.Web.Script.Serialization.JavaScriptSerializer{MaxJsonLength=16000000}.Serialize(timeline));
                    Check(trajectoryError<0.001 && footSkinAgreement<0.01,name+": authored trajectory survives contact IK and native export; both feet agree with native skin throughout the quarter-sampled clip",new{TrajectoryError=trajectoryError,FootSkinAgreement=footSkinAgreement,Samples=timeline.Count});
                    Check(rawTravelZError==0,name+": supplied native actor trajectory keeps source Z exactly",new{MaximumZError=rawTravelZError});
                    if(name=="JumpF" || name=="Fall")Check(!result.Contacts.FloorEnabled && maxAirContact==0 && airFkError<0.001 && airNativeIntegerError<0.06,
                        name+": supplied game-driven flight has no credible ground patch; Auto releases both feet and preserves mapped FK through the entire native clip",
                        new{result.Contacts.FloorEnabled,MaximumAirContact=maxAirContact,SolvedFkError=airFkError,NativeIntegerFkError=airNativeIntegerError});
                    if(name=="Run" || name=="JumpSquat" || name=="LandingAirN")Check(result.Contacts.FloorEnabled && feet>0,name+": Auto detects actual source ground support without a clip mode preset",new{SupportedSamples=feet});
                    measures.Add(measurement);
                    Console.WriteLine(new System.Web.Script.Serialization.JavaScriptSerializer().Serialize(measurement));
                    if(name=="Wait1")Check(allFootClearance<0.04,"The supplied grounded idle keeps both actual soles at the floor throughout the complete native clip, including unfiltered middle frames",measurement);
                    Check(limbError<0.01,name+": contact correction preserves bone offsets, authored scales and conformal limb lengths",measurement);
                    if(name.StartsWith("Cliff",StringComparison.Ordinal))
                    {
                        var ending=BrawlRetargeter.Sample(target,saved,clip.FrameCount-1);
                        Check(new[]{"left.foot","right.foot"}.All(r=>Math.Abs(target.Rig.Surfaces[r].Clearance(ending))<0.03f),
                            name+": supplied source returns to grounded standing; both native target soles finish at the same floor as idle without pose substitution");
                        Check(result.Contacts.Suspended!=null,name+": source geometry automatically identifies hanging ledge support");
                        Check(solverGripError<0.03 && nativeGripError<0.2,name+": hanging grips survive proportion correction and native interpolation",new{Solver=solverGripError,Native=nativeGripError});
                        Check(SolvedHipBelow(result),name+": initial hanging body remains below the platform instead of being planted on a full floor");
                    }
                    if(name=="CliffClimbQuick")
                    {
                        string oldName=clip.Name;
                        try
                        {
                            clip.Name="UnrelatedStandingMotion";
                            var renamed=new ContactMotion(result.Plan,t=>BrawlRetargeter.Sample(source,clip,t),clip.FrameCount-1,true,requireFloorEvidence:true);
                            Check(renamed.Suspended!=null && renamed.Evaluate(0).Pose.World.SequenceEqual(result.Contacts.Evaluate(0).Pose.World),
                                "Renaming a suspended-to-standing motion leaves automatic support and initial pose unchanged");
                        }
                        finally{clip.Name=oldName;}
                    }
                    // A lifted moving foot is not a planted contact. Native cubic
                    // approximation gets the existing 1-unit pose tolerance;
                    // all idles retain the stricter quarter-frame height limit.
                    Check(nearFootSolverExcess<0.03 && nearFootIntegerExcess<0.03 && nearFootExcess<(name.StartsWith("Wait",StringComparison.Ordinal)?0.2:1.0) && kneePoleDot>0.95,
                        name+": low foot lifts retain source proportions at solved/integer poses and supported knees retain their bend plane",measurement);
                    Check(after<=before+0.1,name+": fresh native sole/palm contacts reduce floor error",measurement);
                    double skinAgreement=0;
                    foreach(float time in new[]{0f,(clip.FrameCount-1)*0.37f,clip.FrameCount-1f})
                    {
                        nativeTarget.ApplyCHR(saved,time+1);
                        RigPose pose=BrawlRetargeter.Sample(target,saved,time);
                        foreach(string role in target.Rig.Surfaces.Keys)
                        {
                            var vertices=nativeSurfaces[role];
                            float native=vertices.Min(v=>v.WeightedPosition._y)-target.Rig.GroundHeight;
                            skinAgreement=Math.Max(skinAgreement,Math.Abs(native-target.Rig.Surfaces[role].Sample(pose).Clearance));
                        }
                    }
                    Check(skinAgreement<0.01,name+": contact geometry agrees with independent native skinned vertices",new{MaximumError=skinAgreement});
                    contactsPass&=foot<0.2 && hand<0.2 && body<0.2 && lockError<0.2 && endInteger<0.03 && endIntegerLock<0.03 && bodyIntegerLock<0.2 && endSolved<0.02 && bodyInteger<0.1 && bodySolved<0.1;
                    if(name=="Wait1")Check(feet>0 && locks>0,"Real idle detects planted foot contacts and locks",measurement);
                    if(name=="LandingAirN"||name=="DownStandU")Check(hands>0,"Real "+name+" detects palm/finger ground contacts",measurement);
                    if(name=="AttackAirF")Check(!result.Contacts.FloorEnabled && feet==0 && hands==0,"Air-only native motion is not forced onto a floor",measurement);
                    if(name=="DownWaitU"||name=="DownWaitD")Check(bodies>0,"Real "+name+" automatically detects lying body support",measurement);
                    if(name=="SlipWait")Check(result.Contacts.At("pelvis",0).Support>0 || bodies>0,"Real SlipWait detects floor-seated body support",measurement);
                }
            }
        }
        File.WriteAllText(Path.Combine(output,"contact-measurements.json"),new System.Web.Script.Serialization.JavaScriptSerializer().Serialize(measures));
        Check(contactsPass,"Native quarter-frame height/lock and body locks below 0.2; integer ends/end locks below 0.03, end solver below 0.02 and body integer/solver below 0.1",measures);
    }
    private static Vector3 KneeBend(RigPose pose,string side)
    {
        Vector3 axis=Vector3.Normalize(pose.Point(side+".foot")-pose.Point(side+".upperLeg"));
        Vector3 bend=pose.Point(side+".lowerLeg")-pose.Point(side+".upperLeg");
        return bend-axis*Vector3.Dot(bend,axis);
    }
    private static bool SolvedHipBelow(RetargetResult result)
    {var pose=result.Contacts.Evaluate(0).Pose;return Vector3.Dot(pose.Point("pelvis")-result.Contacts.Suspended.TargetReference,pose.Rig.GroundNormal)<0;}
    private static bool DescendsFrom(MDL0BoneNode bone,MDL0BoneNode ancestor)
    {while(bone!=null){if(ReferenceEquals(bone,ancestor))return true;bone=bone.Parent as MDL0BoneNode;}return false;}
    private static bool NativeSurfaceMember(BrawlLib.Modeling.Vertex3 vertex,BrawlRig rig,string role,MDL0BoneNode end)
    {
        var weights=vertex.GetMatrixNode()?.Weights;if(weights==null)return false;
        if(role.EndsWith(".hand")||role.EndsWith(".foot"))return weights.Where(w=>DescendsFrom(w.Bone as MDL0BoneNode,end)).Sum(w=>w.Weight)>=0.8f;
        if(weights.Any(w=>w.Weight>0.01f && BrawlRetargeter.IsAccessoryBranch(rig.Rig,Array.IndexOf(rig.Bones,w.Bone))))return false;
        var regions=weights.Where(w=>w.Bone is MDL0BoneNode && w.Weight>0).Select(w=>
        {
            var bone=w.Bone as MDL0BoneNode;string name=null;
            while(bone!=null && name==null){int index=Array.IndexOf(rig.Bones,bone);name=rig.Rig.Roles.Keys.FirstOrDefault(r=>rig.Rig.Roles[r]==index);bone=bone.Parent as MDL0BoneNode;}
            return new{Role=name??"",w.Weight};
        }).GroupBy(w=>w.Role).Select(g=>new{Role=g.Key,Weight=g.Sum(w=>w.Weight)}).OrderByDescending(g=>g.Weight).FirstOrDefault();
        if(regions==null || regions.Weight<0.35f)return false;
        string mapped=regions.Role;
        if(new[]{"pelvis","spine","chest","neck"}.Contains(mapped))mapped="pelvis";
        foreach(string side in new[]{"left","right"})
        {
            if(new[]{side+".shoulder",side+".upperArm",side+".lowerArm"}.Contains(mapped))mapped=side+".lowerArm";
            if(new[]{side+".upperLeg",side+".lowerLeg"}.Contains(mapped))mapped=side+".lowerLeg";
        }
        return mapped==role;
    }
}
