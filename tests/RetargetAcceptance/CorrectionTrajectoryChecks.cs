using BrawlCrate.Studio;
using BrawlCrate.Studio.Retargeting;
using BrawlLib.SSBB.ResourceNodes;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Threading;
using System.Web.Script.Serialization;

internal static partial class Program
{
    static void CorrectionTrajectoryChecks()
    {
        foreach(bool renamed in new[]{false,true})
        {
            var joints=Enumerable.Range(0,4).Select(i=>new RigJoint((renamed?"Unfamiliar_":"Joint_")+i,i==0?-1:i-1,
                new JointTransform(new Vector3(0,i==0?0:1,0),Quaternion.Identity,Vector3.One))).ToArray();
            var rig=new Rig("Independent correction fixture","Original fixture",joints,new Dictionary<string,int>(),1);
            float Pulse(float t)=>t>.01f&&t<.02f?(float)Math.Sin((t-.01f)*Math.PI/.01f)*.4f:0;
            RigPose Fk(float t)
            {
                var local=joints.Select(j=>j.Bind).ToArray();local[0].Translation=new Vector3(t*3,0,0);
                local[0].Rotation=Quaternion.CreateFromAxisAngle(Vector3.UnitY,t*(float)Math.PI*4);
                local[1].Translation+=new Vector3(Pulse(t),0,0);
                local[2].Rotation=Quaternion.CreateFromAxisAngle(Vector3.UnitX,t*.7f);
                local[2].Scale=new Vector3(-1,2,.5f);
                return new RigPose(rig,local);
            }
            AdaptiveRotationPose Anchor(float t)
            {
                var p=Fk(t);p.Local[1].Rotation=Quaternion.CreateFromAxisAngle(Vector3.UnitZ,t*.2f)*p.Local[1].Rotation;
                p.Local[1].Translation+=new Vector3(0,t*.1f,0);p.Update();
                return new AdaptiveRotationPose{EulerDegrees=p.Local.Select(v=>BrawlRetargeter.Euler(v.Rotation)).ToArray(),LocalRotations=p.Local.Select(v=>v.Rotation).ToArray(),
                    Translations=p.Local.Select(v=>v.Translation).ToArray(),Scales=p.Local.Select(v=>v.Scale).ToArray(),WorldRotations=p.Orientation};
            }
            var anchors=Enumerable.Range(0,9).Select(i=>Anchor(i/8f)).ToArray();
            var trajectory=new RetargetCorrectionTrajectory(rig,anchors,8,new[]{false,true,true,false},Fk,(b,t)=>b==0?new Vector3(0,t*720,0):Vector3.Zero,CancellationToken.None);
            Check(Enumerable.Range(0,9).All(i=>ReferenceEquals(trajectory.Evaluate(i/8f),anchors[i])),"Correction trajectory retains every exact original solved anchor on unfamiliar rigs");
            foreach(float t in new[]{.015f,.137f,.371f,.623f,.913f})
            {
                var actual=trajectory.Evaluate(t);var expected=Anchor(t);var raw=Fk(t);
                Check(SpanAngle(actual.LocalRotations[1],expected.LocalRotations[1])<.00002&&Vector3.Distance(actual.Translations[1],expected.Translations[1])<.000001,
                    "Transported correction matches an independent analytic rotation/translation and retains narrow source-FK motion",new{renamed,t});
                Check(actual.LocalRotations[0]==raw.Local[0].Rotation&&actual.Translations[0]==raw.Local[0].Translation&&actual.Scales.SequenceEqual(raw.Local.Select(v=>v.Scale)),
                    "Correction authoring preserves exact motion-control locals and original negative/nonuniform scales");
            }
            bool cancelled=false;using(var cancel=new CancellationTokenSource()){cancel.Cancel();try{new RetargetCorrectionTrajectory(rig,anchors,8,new[]{false,true,true,false},Fk,(b,t)=>Vector3.Zero,cancel.Token);}catch(OperationCanceledException){cancelled=true;}}
            Check(cancelled,"Correction reference preparation cancels before publication without changing any solved anchor");
        }
        AdaptiveRotationPose Jump(float t)
        {
            var euler=new Vector3(t<.5025f?0:5,0,0);var q=Quaternion.CreateFromAxisAngle(Vector3.UnitX,euler.X*(float)Math.PI/180);
            return new AdaptiveRotationPose{EulerDegrees=new[]{euler},LocalRotations=new[]{q},WorldRotations=new[]{q}};
        }
        var failed=AdaptiveRotationCurveBaker.Bake(new[]{-1},new[]{true},1,Jump,(b,t)=>Quaternion.Identity,CancellationToken.None);
        Check(!failed.Converged&&!failed.WorkLimitReached&&failed.UnresolvedIntervals==1&&failed.Work<50000,
            "A genuinely discontinuous solved reference refuses at its first terminal interval without spending the remaining operation budget",new{failed.Work,failed.SampleCount});
        AdaptiveRotationPose Chain(float t)
        {
            var angle=(float)Math.Sin(t*5)*20;var q=Quaternion.CreateFromAxisAngle(Vector3.UnitY,angle*(float)Math.PI/180);
            return new AdaptiveRotationPose{EulerDegrees=Enumerable.Range(0,8).Select(b=>b==0?new Vector3(0,angle,0):Vector3.Zero).ToArray(),
                LocalRotations=Enumerable.Range(0,8).Select(b=>b==0?q:Quaternion.Identity).ToArray(),WorldRotations=Enumerable.Repeat(q,8).ToArray()};
        }
        var chain=AdaptiveRotationCurveBaker.Bake(Enumerable.Range(0,8).Select(b=>b-1).ToArray(),Enumerable.Repeat(true,8).ToArray(),2,Chain,(b,t)=>Quaternion.Identity,CancellationToken.None);
        Check(chain.Converged&&chain.Tracks.Length==8&&chain.MaximumOrientationDegrees<=.025,
            "Ancestor fitting reserves descendant headroom instead of repeatedly subdividing a constant child's inherited orientation error",new{chain.Work,chain.MaximumOrientationDegrees});
        const float centre=.2422638f,width=.00002f;
        AdaptiveRotationPose Narrow(float time)
        {
            float angle=Math.Max(0,1-Math.Abs(time-centre)/width)*.1f;
            var q=Quaternion.CreateFromAxisAngle(Vector3.UnitZ,angle*(float)Math.PI/180);
            return new AdaptiveRotationPose{EulerDegrees=new[]{Vector3.Zero,new Vector3(0,0,angle),Vector3.Zero},
                LocalRotations=new[]{Quaternion.Identity,q,Quaternion.Identity},WorldRotations=new[]{Quaternion.Identity,q,q}};
        }
        var narrow=AdaptiveRotationCurveBaker.Bake(new[]{-1,0,1},new[]{false,true,true},1,Narrow,(b,t)=>Quaternion.Identity,CancellationToken.None,
            mandatoryTimes:new[]{centre-width,centre,centre+width});
        Check(narrow.Converged&&narrow.MaximumOrientationDegrees<=.025,"Mandatory source/correction breakpoints expose narrow parent motion before a child inherits an unchecked error",new{narrow.Work,narrow.SampleCount});
        using(var clip=new CHR0Node{Name="Independent narrow parent",Version=4,FrameCount=2})
        {
            var entry=clip.CreateEntry("Unfamiliar parent");entry.Keyframes.PreserveInterpolation=true;
            foreach(var key in narrow.Tracks.Single(t=>t.Bone==1).Rotation)
            {
                entry.SetKeyframe(5,key.Time,key.Value.Z,true,true)._tangent=key.IncomingSlope.Z;
                if(key.IncomingSlope.Z!=key.OutgoingSlope.Z)entry.SetKeyframe(5,key.Time,key.Value.Z,true,true)._tangent=key.OutgoingSlope.Z;
            }
            double maximum=0;
            foreach(float time in new[]{centre-width,centre-width*.85f,centre-width*.37f,centre,centre+width*.18f,centre+width*.73f,centre+width})
                maximum=Math.Max(maximum,SpanAngle(BrawlRetargeter.Rotation(new BrawlLib.Internal.Vector3(0,0,entry.GetFrameValue(5,time))),Narrow(time).WorldRotations[1]));
            Check(maximum<=.025,"Independent native interpolation preserves narrow mandatory-knot motion at withheld subframes",new{maximum});
        }
    }

    static int CompareContactProposal(string sourcePath,string targetPath,string motionPath,string name,string output)
    {
        Directory.CreateDirectory(output);
        using(var s=NodeFactory.FromFile(null,sourcePath))using(var t=NodeFactory.FromFile(null,targetPath))using(var m=NodeFactory.FromFile(null,motionPath))
        {
            var source=BrawlRetargeter.Capture(Walk(s).OfType<MDL0Node>().First());var target=BrawlRetargeter.Capture(Walk(t).OfType<MDL0Node>().First());
            var clip=Walk(m).OfType<CHR0Node>().Single(c=>c.Name==name);var times=new List<long>();var bytes=new List<byte[]>();
            foreach(bool legacy in new[]{false,true})
            {
                var watch=System.Diagnostics.Stopwatch.StartNew();
                using(var result=BrawlRetargeter.BakeCore(source,target,clip,true,CancellationToken.None,null,GroundContactMode.Auto,RetargetKeyframeMode.Dense,legacy))
                {
                    times.Add(watch.ElapsedMilliseconds);string path=Path.Combine(output,legacy?"with-obsolete-proposal.chr0":"without-obsolete-proposal.chr0");result.Animation.ExportChecked(path);
                    using(var fresh=NodeFactory.FromFile(null,path) as CHR0Node)Check(fresh!=null&&NativeAnimationOptimization.SourceContentKey(fresh)==NativeAnimationOptimization.SourceContentKey(result.Animation),"A/B master independently reopens with exact native curves/flags");
                    bytes.Add(File.ReadAllBytes(path));
                }
            }
            Check(bytes[0].SequenceEqual(bytes[1]),"Removing overwritten contact tangent proposals preserves complete serialized CHR0 bytes");
            File.WriteAllText(Path.Combine(output,"proposal-comparison.json"),new JavaScriptSerializer().Serialize(new{Passed=true,WithoutMilliseconds=times[0],WithMilliseconds=times[1],Bytes=bytes[0].Length,ExactSerializedEquality=true}));return 0;
        }
    }

    static int VerifyRetargetTrajectory(string sourcePath,string targetPath,string motionPath,string name,string animationPath,string output)
    {
        Directory.CreateDirectory(output);
        using(var s=NodeFactory.FromFile(null,sourcePath))using(var t=NodeFactory.FromFile(null,targetPath))using(var m=NodeFactory.FromFile(null,motionPath))using(var saved=NodeFactory.FromFile(null,animationPath) as CHR0Node)
        {
            var source=BrawlRetargeter.Capture(Walk(s).OfType<MDL0Node>().First(n=>!n.Name.ToLowerInvariant().Contains("shadow")));
            var target=BrawlRetargeter.Capture(Walk(t).OfType<MDL0Node>().First(n=>!n.Name.ToLowerInvariant().Contains("shadow")));
            var clip=Walk(m).OfType<CHR0Node>().Single(c=>c.Name==name);var plan=new HumanoidRetargeter(source.Rig,target.Rig);
            int last=clip.Loop?clip.FrameCount:clip.FrameCount-1;
            var contacts=new ContactMotion(plan,time=>BrawlRetargeter.Sample(source,clip,time),last,true,requireFloorEvidence:true,loop:clip.Loop);
            double supportOrientation=0,intendedOrientation=0;float orientationTime=0;string orientationRole=null;
            var orientationWitnesses=new List<object>();
            double rawLockDrift=0;float lockTime=0;string lockRole=null;
            double maxAngle=0,maxTranslation=0,maxScale=0,maxWorld=0,rootError=0,penetration=0,rawPenetration=0,lockDrift=0,nativePoseError=0,nativeSurfaceError=0,nativePenetration=0;int samples=0,contactSamples=0;float penetrationTime=0;string penetrationRole=null;
            var nativeTarget=target.Bones[0].Model;
            var nativeSurfaces=target.Rig.Surfaces.Keys.ToDictionary(role=>role,role=>nativeTarget.PolygonList.Cast<MDL0ObjectNode>().SelectMany(o=>o.Vertices)
                .Where(v=>NativeSurfaceMember(v,target,role,target.Bones[target.Rig.Roles[role]])).ToArray());
            double contactBound=new[]{"left","right"}.Min(side=>Vector3.Distance(target.Rig.Rest.Point(side+".upperLeg"),target.Rig.Rest.Point(side+".lowerLeg"))+
                Vector3.Distance(target.Rig.Rest.Point(side+".lowerLeg"),target.Rig.Rest.Point(side+".foot")))*.001;
            var times=new SortedSet<float>(Enumerable.Range(0,last*8+1).Select(i=>i/8f));
            foreach(int i in Enumerable.Range(0,last))foreach(float fraction in new[]{.137f,.371f,.623f})times.Add(i+fraction);
            var regions=target.Rig.Surfaces.ToDictionary(p=>p.Key,p=>p.Value.Points);
            foreach(float time in times)
            {
                var expected=contacts.Evaluate(time).Pose;var actual=BrawlRetargeter.Sample(target,saved,time);samples++;
                var intended=contacts.EndOrientationReference(time);
                nativeTarget.ApplyCHR(saved,time+1);
                bool anchor=time*8==(int)(time*8);
                for(int bone=0;bone<target.Bones.Length;bone++)
                {
                    if(anchor){maxAngle=Math.Max(maxAngle,SpanAngle(actual.Orientation[bone],expected.Orientation[bone]));maxTranslation=Math.Max(maxTranslation,Vector3.Distance(actual.Local[bone].Translation,expected.Local[bone].Translation));maxScale=Math.Max(maxScale,Vector3.Distance(actual.Local[bone].Scale,expected.Local[bone].Scale));maxWorld=Math.Max(maxWorld,Vector3.Distance(actual.Point(bone),expected.Point(bone)));}
                    if(plan.TargetMotionControls.Contains(bone))rootError=Math.Max(rootError,Math.Max(SpanAngle(actual.Local[bone].Rotation,expected.Local[bone].Rotation),Vector3.Distance(actual.Local[bone].Translation,expected.Local[bone].Translation)));
                    nativePoseError=Math.Max(nativePoseError,Vector3.Distance(actual.Point(bone),BrawlRetargeter.Vector(target.Bones[bone].Matrix.GetPoint())));
                }
                foreach(var pair in regions)
                {
                    var goal=contacts.At(pair.Key,time);if(goal.Support<=.99f)continue;contactSamples++;
                    if(pair.Key.EndsWith(".foot",StringComparison.Ordinal)||pair.Key.EndsWith(".hand",StringComparison.Ordinal))
                    {
                        int joint=target.Rig.Roles[pair.Key];double error=SpanAngle(actual.Orientation[joint],expected.Orientation[joint]);
                        if(error>supportOrientation){supportOrientation=error;orientationTime=time;orientationRole=pair.Key;}
                        if(!intended.TryGetValue(pair.Key,out var goalOrientation))throw new InvalidDataException("Supported end has no explicit intended orientation.");
                        intendedOrientation=Math.Max(intendedOrientation,SpanAngle(actual.Orientation[joint],goalOrientation));
                        orientationWitnesses.Add(new{Time=time,Role=pair.Key,ErrorDegrees=error,Actual=actual.Orientation[joint],Expected=expected.Orientation[joint]});
                    }
                    float low=pair.Value.Min(p=>Vector3.Dot(p.Evaluate(actual),target.Rig.GroundNormal))-target.Rig.GroundHeight;
                    float raw=pair.Value.Min(p=>Vector3.Dot(p.Evaluate(expected),target.Rig.GroundNormal))-target.Rig.GroundHeight;
                    if(-low>penetration){penetrationTime=time;penetrationRole=pair.Key;}penetration=Math.Max(penetration,Math.Max(0,-low));rawPenetration=Math.Max(rawPenetration,Math.Max(0,-raw));
                    if(nativeSurfaces[pair.Key].Length>0){float nativeLow=nativeSurfaces[pair.Key].Min(v=>Vector3.Dot(BrawlRetargeter.Vector(v.WeightedPosition),target.Rig.GroundNormal))-target.Rig.GroundHeight;
                        nativeSurfaceError=Math.Max(nativeSurfaceError,Math.Abs(nativeLow-low));nativePenetration=Math.Max(nativePenetration,Math.Max(0,-nativeLow));}
                    if(goal.Marker!=null&&goal.Lock>.99f){var delta=goal.Marker.Evaluate(actual)-goal.Anchor;delta-=target.Rig.GroundNormal*Vector3.Dot(delta,target.Rig.GroundNormal);if(delta.Length()>lockDrift){lockDrift=delta.Length();lockTime=time;lockRole=pair.Key;} var rawDelta=goal.Marker.Evaluate(expected)-goal.Anchor;rawDelta-=target.Rig.GroundNormal*Vector3.Dot(rawDelta,target.Rig.GroundNormal);rawLockDrift=Math.Max(rawLockDrift,rawDelta.Length());}
                }
            }
            var report=new{Clip=name,Passed=maxAngle<=.025&&maxTranslation<=.0002&&maxScale<=.00001&&rootError<.0001&&nativePoseError<.01&&nativeSurfaceError<.01&&penetration<=contactBound&&nativePenetration<=contactBound&&lockDrift<=contactBound&&supportOrientation<=.25,Samples=samples,OriginalSolvedAnchors=last*8+1,MaximumAnchorOrientation=maxAngle,MaximumAnchorTranslation=maxTranslation,MaximumAnchorScale=maxScale,MaximumAnchorWorldPosition=maxWorld,RootLocalError=rootError,FullContactSamples=contactSamples,MaximumPenetration=penetration,PenetrationTime=penetrationTime,PenetrationRole=penetrationRole,NativePenetration=nativePenetration,ContactPositionBound=contactBound,NativeBoneAgreement=nativePoseError,NativeSurfaceAgreement=nativeSurfaceError,RawSolverPenetration=rawPenetration,MaximumPlanarLockDrift=lockDrift,RawPlanarLockDrift=rawLockDrift,PlanarLockTime=lockTime,PlanarLockRole=lockRole,SupportOrientationAgainstRawNext=supportOrientation,SupportOrientationTime=orientationTime,SupportOrientationRole=orientationRole,AllSkinInfluences=true,ContinuousProof=false,RawSubframeOptimaReference=false};
            File.WriteAllText(Path.Combine(output,"trajectory-verification.json"),new JavaScriptSerializer().Serialize(report));
            File.WriteAllText(Path.Combine(output,"intended-orientation-verification.json"),new JavaScriptSerializer().Serialize(new{Passed=intendedOrientation<=.25,MaximumDegrees=intendedOrientation,LimitDegrees=.25,Reference=ContactMotion.EndOrientationPolicy}));
            File.WriteAllText(Path.Combine(output,"support-orientation-witnesses.json"),new JavaScriptSerializer().Serialize(orientationWitnesses));
            Check(report.Passed,"Fresh native CHR0 retains original solved eighth-frame poses, native timing and source-owned motion controls",report);
            Check(intendedOrientation<=.25,"Decoded supported end orientations retain the intended pre-coupled contact goal within the unchanged bound",new{intendedOrientation});
            return 0;
        }
    }
}
