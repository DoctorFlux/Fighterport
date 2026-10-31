using BrawlCrate.Studio.Retargeting;
using BrawlLib.SSBB.ResourceNodes;
using BrawlLib.Wii.Animations;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Threading;
using System.Web.Script.Serialization;

internal static partial class Program
{
    static int AdaptiveBudgetChecks(string output,bool useProofs=true)
    {
        Directory.CreateDirectory(output);
        const int count=128,last=160;
        var fixture=CoordinateFixture("opaque-budget-",1,.3f,0,Quaternion.Identity);
        var joints=fixture.Rig.Joints.Concat(Enumerable.Range(20,count-20).Select(b=>new RigJoint("anonymous-helper-"+b,
            b<24?1:b-4,new JointTransform(new Vector3(.1f,1,.07f),Quaternion.Identity,Vector3.One)))).ToArray();
        var rig=new Rig("Independent budget fixture","Original procedural hierarchy",joints,fixture.Rig.Roles,1,Vector3.UnitY);
        var plan=new HumanoidRetargeter(rig,rig);var mapped=new HashSet<int>(plan.Mapping.Select(m=>m.TargetIndex));
        bool[] editable=Enumerable.Range(0,count).Select(b=>mapped.Contains(b)&&!plan.TargetMotionControls.Contains(b)).ToArray();
        Quaternion Frozen(int b,float t)=>b==0?Quaternion.CreateFromAxisAngle(Vector3.UnitY,t/last*(float)Math.PI*4):joints[b].Bind.Rotation;
        AdaptiveRotationPose Sample(float t)
        {
            var euler=new Vector3[count];var local=new Quaternion[count];var world=new Quaternion[count];
            var translation=joints.Select(j=>j.Bind.Translation).ToArray();var scale=joints.Select(j=>j.Bind.Scale).ToArray();
            for(int b=0;b<count;b++)
            {
                euler[b]=b==0?new Vector3(0,t/last*720,0):!editable[b]?Vector3.Zero:new Vector3((float)Math.Sin(t*.11+b*.13)*15,b%3*21+(float)Math.Cos(t*.073)*5,(float)Math.Sin(t*.12)*9);
                local[b]=b==0?Frozen(b,t):BrawlRetargeter.Rotation(new BrawlLib.Internal.Vector3(euler[b].X,euler[b].Y,euler[b].Z));
                world[b]=Quaternion.Normalize((joints[b].Parent<0?Quaternion.Identity:world[joints[b].Parent])*local[b]);
            }
            translation[0]=new Vector3(t*.03f,0,0);translation[rig.Roles["pelvis"]].Y+=(float)Math.Sin(t*.05)*.003f;
            scale[rig.Roles["right.lowerLeg"]]=new Vector3(-1,2,.5f);
            return new AdaptiveRotationPose{EulerDegrees=euler,LocalRotations=local,WorldRotations=world,Translations=translation,Scales=scale};
        }
        using(var clip=new CHR0Node{Name="Independent generated motion",Version=4,FrameCount=last+1})
        {
            // Missing S/T channels prove source bind fallback, not an endpoint guess.
            foreach(var joint in joints){var entry=clip.CreateEntry(joint.Name);entry.SetKeyframe(3,0,0,true,true);entry.SetKeyframe(3,last,1,true,true);
                if(joint.Name==joints[rig.Roles["right.lowerLeg"]].Name){entry.SetKeyframe(0,0,-1,true,true);entry.SetKeyframe(1,0,2,true,true);entry.SetKeyframe(2,0,.5f,true,true);}}
            var watch=System.Diagnostics.Stopwatch.StartNew();
            var invariants=useProofs?NativeRetargetConstants.Capture(plan,clip,CancellationToken.None):null;
            var mandatory=Enumerable.Range(0,last*8+1).Select(i=>i/8f).ToArray();
            var bake=useProofs?WindowedNativeCurveBaker.Bake(joints.Select(j=>j.Parent).ToArray(),editable,last,Sample,Frozen,CancellationToken.None,mandatory,invariants):
                AdaptiveRotationCurveBaker.Bake(joints.Select(j=>j.Parent).ToArray(),editable,last,Sample,Frozen,CancellationToken.None,generatedEulerContinuation:true,mandatoryTimes:mandatory);
            var report=new{bake.Converged,bake.WorkLimitReached,bake.Work,bake.SampleCount,Tracks=bake.Tracks.Length,bake.UnresolvedIntervals,bake.LastBone,bake.LastChannel,bake.LastMeasuredError,
                bake.ProvenConstantGroups,bake.WindowCount,bake.MaximumWindowWork,Milliseconds=watch.ElapsedMilliseconds,OriginalProcedural=true,TargetBones=count,Frames=last+1,UseProofs=useProofs,Editable=editable.Count(v=>v)};
            File.WriteAllText(Path.Combine(output,"adaptive-budget.json"),new JavaScriptSerializer().Serialize(report));
            Console.WriteLine(new JavaScriptSerializer().Serialize(report));
            if(bake.Converged)
            {
                VerifyBudgetNative(rig,bake,last,Sample,Frozen,Path.Combine(output,"budget-master.chr0"));
                using(var stop=new CancellationTokenSource())
                {
                    bool cancelled=false;
                    try{WindowedNativeCurveBaker.Bake(joints.Select(j=>j.Parent).ToArray(),editable,last,Sample,Frozen,stop.Token,mandatory,invariants,(i,n)=>{if(i==1)stop.Cancel();});}
                    catch(OperationCanceledException){cancelled=true;}
                    Check(cancelled,"Cancellation between bounded sections returns no partial master");
                }
                NativeConstantProofChecks(plan,clip);
                WindowBoundaryChecks(output);
                File.WriteAllText(Path.Combine(output,"checks.json"),new JavaScriptSerializer().Serialize(new{Passed=true,Checks}));
            }
            return bake.Converged?0:1;
        }
    }
    static void VerifyBudgetNative(Rig rig,AdaptiveRotationBake bake,int last,Func<float,AdaptiveRotationPose> sample,Func<int,float,Quaternion> frozen,string file)
    {
        using(var native=new CHR0Node{Name="Independent bounded native master",Version=4,FrameCount=last+1})
        {
            var root=native.CreateEntry(rig.Joints[0].Name);root.Keyframes.PreserveInterpolation=true;
            if(last>0){root.SetKeyframe(4,0,0,true,true)._tangent=720f/last;root.SetKeyframe(4,last,720,true,true)._tangent=720f/last;
                root.SetKeyframe(6,0,0,true,true)._tangent=.03f;root.SetKeyframe(6,last,last*.03f,true,true)._tangent=.03f;}
            foreach(var track in bake.Tracks)
            {
                var entry=native.CreateEntry(rig.Joints[track.Bone].Name);entry.Keyframes.PreserveInterpolation=true;
                foreach(var group in new[]{Tuple.Create(0,track.Scale),Tuple.Create(3,track.Rotation),Tuple.Create(6,track.Translation)})
                    if(group.Item2!=null)foreach(var key in group.Item2)for(int axis=0;axis<3;axis++)
                    {
                        float C(Vector3 v)=>axis==0?v.X:axis==1?v.Y:v.Z;
                        entry.SetKeyframe(group.Item1+axis,key.Time,C(key.Value),true,true)._tangent=C(key.IncomingSlope);
                        if(BitConverter.ToInt32(BitConverter.GetBytes(C(key.IncomingSlope)),0)!=BitConverter.ToInt32(BitConverter.GetBytes(C(key.OutgoingSlope)),0))
                            entry.SetKeyframe(group.Item1+axis,key.Time,C(key.Value),true,true)._tangent=C(key.OutgoingSlope);
                    }
            }
            PreciseCHR0EntryNode.Promote(native);native.ExportChecked(file);
            using(var decoded=NodeFactory.FromFile(null,file) as CHR0Node)
            {
                var entries=decoded.Children.OfType<CHR0EntryNode>().ToDictionary(e=>e.Name);
                double angle=0,translation=0,scale=0,rootError=0;int samples=0;
                var times=new SortedSet<float>(Enumerable.Range(0,last+1).Select(i=>(float)i));
                for(int frame=0;frame<last;frame++)foreach(float sub in new[]{.137f,.371f,.623f})times.Add(frame+sub);
                for(int seam=8;seam<last;seam+=8)foreach(float sub in new[]{-.001f,0,.001f})times.Add(seam+sub);
                foreach(float time in times)
                {
                    var expected=sample(time);var world=new Quaternion[rig.Joints.Length];
                    for(int bone=0;bone<rig.Joints.Length;bone++)
                    {
                        var bind=rig.Joints[bone].Bind;entries.TryGetValue(rig.Joints[bone].Name,out var entry);
                        float V(int c,float fallback)=>entry!=null&&entry.KeyArrays[c].KeyCount>0?entry.GetFrameValue(c,time):fallback;
                        var q=BrawlRetargeter.Rotation(new BrawlLib.Internal.Vector3(V(3,0),V(4,0),V(5,0)));
                        world[bone]=Quaternion.Normalize((rig.Joints[bone].Parent<0?Quaternion.Identity:world[rig.Joints[bone].Parent])*q);
                        angle=Math.Max(angle,SpanAngle(world[bone],expected.WorldRotations[bone]));
                        translation=Math.Max(translation,Vector3.Distance(new Vector3(V(6,bind.Translation.X),V(7,bind.Translation.Y),V(8,bind.Translation.Z)),expected.Translations[bone]));
                        var delta=new Vector3(V(0,bind.Scale.X),V(1,bind.Scale.Y),V(2,bind.Scale.Z))-expected.Scales[bone];scale=Math.Max(scale,Math.Max(Math.Abs(delta.X),Math.Max(Math.Abs(delta.Y),Math.Abs(delta.Z))));
                        if(bone==0)rootError=Math.Max(rootError,SpanAngle(q,frozen(bone,time)));
                    }
                    samples++;
                }
                Check(angle<=.025&&translation<=.0002&&scale<=.00001&&rootError<.0001,"Independent fresh native decode retains whole-hierarchy quality and protected root travel/720-degree winding across every bounded join",new{samples,angle,translation,scale,rootError});
                Check(decoded.FrameCount==last+1&&!decoded.Loop&&decoded.Version==4,"Bounded master retains native duration, version and terminal semantics");
            }
        }
    }
    static void NativeConstantProofChecks(HumanoidRetargeter plan,CHR0Node clip)
    {
        int bone=plan.Target.Roles["left.lowerLeg"];
        var entry=(CHR0EntryNode)clip.Children.Single(e=>e.Name==plan.Source.Joints[plan.Source.Roles["left.lowerLeg"]].Name);
        var a=entry.SetKeyframe(6,0,3,true,true);var b=entry.SetKeyframe(6,1,3,true,true);a._tangent=b._tangent=0;
        var proof=NativeRetargetConstants.Capture(plan,clip,CancellationToken.None);
        Check(proof.Translation(bone)&&!proof.Translation(plan.Target.Roles["pelvis"])&&plan.TargetMotionControls.All(i=>!proof.Scale(i)&&!proof.Translation(i)),"Exact native constant proof includes absent bind axes but excludes solved pelvis translation and every protected control");
        a._tangent=2;
        Check(!NativeRetargetConstants.Capture(plan,clip,CancellationToken.None).Translation(bone)&&entry.GetFrameValue(6,.5f)!=3,"Equal stored values with nonzero Hermite tangents never establish constancy");
        a._tangent=BitConverter.ToSingle(BitConverter.GetBytes(int.MinValue),0);
        Check(!NativeRetargetConstants.Capture(plan,clip,CancellationToken.None).Translation(bone),"Uncertain signed-zero derivative semantics retain the ordinary native path");
        a._tangent=0;proof=NativeRetargetConstants.Capture(plan,clip,CancellationToken.None);b._value=4;bool stale=false;
        try{proof.Validate(plan.Target.Joints.Select(j=>j.Parent).ToArray());}catch(InvalidDataException){stale=true;}
        Check(stale,"Native constant proof rejects changed exact source content before reuse");
    }
    static void WindowBoundaryChecks(string output)
    {
        foreach(bool turns in new[]{false,true})foreach(bool loop in new[]{false,true})
        {
            const int last=17;var joints=new[]{new RigJoint("anonymous-control",-1,new JointTransform(Vector3.Zero,Quaternion.Identity,Vector3.One)),new RigJoint("anonymous-body",0,new JointTransform(Vector3.UnitY,Quaternion.Identity,Vector3.One))};
            var rig=new Rig("Independent seam","Original procedural",joints,new Dictionary<string,int>(),1);
            Quaternion Frozen(int b,float t)=>Quaternion.Identity;
            AdaptiveRotationPose Sample(float t)
            {
                double d=t-8.001,w=.001;float angle=(float)(Math.Atan2(w,d)*180/Math.PI);
                var e=turns?new Vector3(0,0,t/last*720):new Vector3(angle,(float)(-90+Math.Sqrt(d*d+w*w)*.1),-angle);
                var q=BrawlRetargeter.Rotation(new BrawlLib.Internal.Vector3(e.X,e.Y,e.Z));
                return new AdaptiveRotationPose{EulerDegrees=new[]{Vector3.Zero,e},LocalRotations=new[]{Quaternion.Identity,q},WorldRotations=new[]{Quaternion.Identity,q},
                    Translations=new[]{new Vector3(t*.03f,0,0),Vector3.UnitY},Scales=new[]{Vector3.One,Vector3.One}};
            }
            var baked=WindowedNativeCurveBaker.Bake(new[]{-1,0},new[]{false,true},last,Sample,Frozen,CancellationToken.None,new[]{8f,8.001f,16f},null);
            Check(baked.Converged&&baked.WindowCount==3&&baked.MaximumWindowWork<=2000000,"Generated Euler continuation crosses native window boundaries without a family reset or loss of intentional full turns",new{turns,baked.Work,baked.MaximumWindowWork});
            if(turns)Check(baked.Tracks.Single().Rotation.Last().Value.Z==720,"Bounded continuation retains a full 720-degree authored body turn");
            // Separate native scalar sampling around the difficult seam.
            using(var native=new CHR0Node{Name="Anonymous seam",FrameCount=loop?last:last+1,Loop=loop})
            {
                var entry=native.CreateEntry("anonymous-body");entry.Keyframes.PreserveInterpolation=true;
                foreach(var key in baked.Tracks.Single().Rotation)for(int c=0;c<3;c++)
                {
                    float V(Vector3 v)=>c==0?v.X:c==1?v.Y:v.Z;
                    entry.SetKeyframe(c+3,key.Time,V(key.Value),true,true)._tangent=V(key.IncomingSlope);
                    if(V(key.IncomingSlope)!=V(key.OutgoingSlope))entry.SetKeyframe(c+3,key.Time,V(key.Value),true,true)._tangent=V(key.OutgoingSlope);
                }
                string file=Path.Combine(output,(turns?"turn":"pole")+(loop?"-loop":"-nonloop")+"-seam.chr0");PreciseCHR0EntryNode.Promote(native);native.ExportChecked(file);
                using(var fresh=NodeFactory.FromFile(null,file) as CHR0Node)
                {
                    var saved=(CHR0EntryNode)fresh.Children[0];double maximum=0;
                    foreach(float time in Enumerable.Range(0,2177).Select(i=>i*17f/2176).Concat(new[]{7.999f,8f,8.0009f,8.001f,8.0011f,15.999f,16f,16.001f}))
                        maximum=Math.Max(maximum,SpanAngle(BrawlRetargeter.Rotation(new BrawlLib.Internal.Vector3(saved.GetFrameValue(3,time),saved.GetFrameValue(4,time),saved.GetFrameValue(5,time))),Sample(time).WorldRotations[1]));
                    Check(maximum<=.025&&fresh.Loop==loop&&fresh.FrameCount==(loop?last:last+1),"Independent serialized native seam checks retain near-pole/full-turn motion and looping/terminal metadata at withheld fractional times",new{turns,loop,maximum});
                }
            }
        }
        AdaptiveRotationPose One(float t)=>new AdaptiveRotationPose{EulerDegrees=new[]{Vector3.Zero},WorldRotations=new[]{Quaternion.Identity}};
        var single=WindowedNativeCurveBaker.Bake(new[]{-1},new[]{true},0,One,(b,t)=>Quaternion.Identity,CancellationToken.None,new[]{0f},null);
        Check(single.Converged&&single.Tracks.Single().Rotation.Length==1&&single.ValidationTimes.SequenceEqual(new[]{0f}),"One-frame native masters keep one exact knot and their original sample semantics");
        // The desired hierarchy starts requiring a different protected parent
        // after the first section. A child may not rewrite that frozen source
        // control or its contractual integer local pose to hide the conflict.
        AdaptiveRotationPose Jump(float t){var e=new Vector3(t<8.5f?0:5,0,0);var q=BrawlRetargeter.Rotation(new BrawlLib.Internal.Vector3(e.X,0,0));return new AdaptiveRotationPose{EulerDegrees=new[]{e,Vector3.Zero},LocalRotations=new[]{q,Quaternion.Identity},WorldRotations=new[]{q,q}};}
        var failure=WindowedNativeCurveBaker.Bake(new[]{-1,0},new[]{false,true},9,Jump,(b,t)=>Quaternion.Identity,CancellationToken.None,new[]{8.5f},null);
        Check(!failure.Converged&&failure.UnresolvedIntervals>0&&!failure.WorkLimitReached&&failure.Tracks.Length==0,"A genuine later-section contradiction discards earlier sections instead of publishing a partial or relaxed master");
    }
}
