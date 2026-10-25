using FighterPort.Core;
using System.Numerics;
using System.Text.Json;

internal static class BoundaryFittingChecks
{
    public static async Task RunAsync(string dir,Action<bool,string> check)
    {
        var scene=new Scene("Original boundary fixture",[new("root",-1,Transform.Identity),new("joint",0,new(Vector3.One,new(0,7,0),new(2,3,4)))],[]);
        var channels=new Curve?[9];channels[3]=new([new(0,0,1,1),new(10,10,1,1),new(15,15,1,1),new(15,15,1,1),new(25,25,1,1)]);channels[6]=new([new(0,2,1,1),new(25,27,1,1)]);
        var root=new Curve?[9];root[6]=new([new(0,9),new(25,99)]);
        var clip=new AnimationClip(Guid.NewGuid(),"Action",26,new(60),false,[new("root",root),new("joint",channels)]);
        var referenceChannels=new Curve?[9];referenceChannels[3]=new([new(0,20,2,2),new(25,70,2,2)]);
        var reference=new AnimationClip(Guid.NewGuid(),"Reference",26,new(60),false,[new("joint",referenceChannels)]);
        var project=new FighterProject(1,Guid.NewGuid(),"Original",scene,[clip,reference]);string original=JsonSerializer.Serialize(project,ProjectStore.Json);
        var fingerprint=AnimationControls.Fingerprint(scene);var entry=new BoundaryAnchor(reference,fingerprint,2);var exit=new BoundaryAnchor(reference,fingerprint,3);
        var options=new BoundaryFitSettings(6,6,TransformComponents.Rotation,["joint"],[new(6,19,"Protected action")]);
        var fit=BoundaryFitting.Prepare(project,clip,entry,exit,options);var curve=fit.Clip.Tracks.Single(t=>t.Bone=="joint").Channels[3]!;
        void Near(float actual,float expected,string text)=>check(Math.Abs(actual-expected)<.00003f,text);
        check(fit.Clip.SampleCount==26&&fit.Clip.Rate==clip.Rate&&fit.Clip.Loop==clip.Loop,"Boundary fit preserves existing duration, timebase and loop flag");
        Near(curve.Sample(0),24,"Entry matches the frozen reference phase");Near(curve.Sample(25),26,"Exit matches the frozen reference phase");
        Near(curve.Sample(2.5),13.875f,"Entry interpolation matches an independent Hermite polynomial");Near(curve.Sample(22.5),23.625f,"Exit interpolation matches an independent Hermite polynomial");
        Near(BoundaryFitting.Derivative(curve,0),0,"Frozen reference has zero endpoint velocity");Near(BoundaryFitting.Derivative(curve,5),1,"Inserted entry join preserves analytic original velocity");Near(BoundaryFitting.Derivative(curve,20),1,"Inserted exit join preserves analytic original velocity");
        check(channels[3]!.Keys.Where(k=>k.Frame>=5&&k.Frame<=20).SequenceEqual(curve.Keys.Where(k=>k.Frame>=5&&k.Frame<=20&&k.Frame!=5&&k.Frame!=20)),"Protected original keys and duplicate sides retain exact values/tangents");
        check(Enumerable.Range(0,601).All(i=>Math.Abs(curve.Sample(5+i/40d)-channels[3]!.Sample(5+i/40d))<.00003),"The complete protected middle curve stays unchanged at independent fractional times");
        check(ReferenceEquals(fit.Clip.Tracks[0],clip.Tracks[0])&&ReferenceEquals(fit.Clip.Tracks[1].Channels[6],channels[6]),"Unselected bones and component curves retain their exact source objects");
        check(fit.Clip.Tracks[1].Channels[4]!.Sample(0)==7&&fit.Clip.Tracks[1].Channels[5]!.Sample(25)==0,"New native component axes use authentic nonzero rest constants");
        check(fit.Clip.Id!=clip.Id&&fit.Clip.Revision!=clip.Revision&&JsonSerializer.Serialize(project,ProjectStore.Json)==original,"Output is an independent revision; the complete input document remains immutable");
        var moving=BoundaryFitting.Prepare(project,clip,entry with{Clip=reference with{Rate=new(120)},Frozen=false,Rate=.5},null,options);
        Near(BoundaryFitting.Derivative(moving.Clip.Tracks[1].Channels[3],0),2,"Moving reference velocity respects both timebases and explicit speed");
        var pair=new Curve([new(0,0,0,1),new(2,3,2,2),new(2,5,3,3),new(4,9,4,0)]);
        Near(BoundaryFitting.Derivative(pair,2,false),2,"Analytic derivative retains the incoming native duplicate side");Near(BoundaryFitting.Derivative(pair,2),3,"Analytic derivative retains the outgoing native duplicate side");
        var linear=new Curve([new(0,2),new(4,10)],Interpolation.Linear);Near(BoundaryFitting.Derivative(linear,1),2,"Reference linear curves have their exact analytic velocity");
        var loop=new Curve([new(1,2,1,1),new(3,4,1,1)],Interpolation.Hermite,5);Near(BoundaryFitting.Derivative(loop,4.5),-1.5f,"Analytic native loop bridge velocity matches an independent polynomial");
        bool Reject(Action action,string expected){try{action();return false;}catch(Exception error)when(error is InvalidDataException or NotSupportedException){return error.Message.Contains(expected,StringComparison.OrdinalIgnoreCase);}}
        check(Reject(()=>BoundaryFitting.Prepare(project,clip,entry,exit,options with{EntrySamples=22}),"overlap"),"Overlapping fit windows reject without changing the action");
        check(Reject(()=>BoundaryFitting.Prepare(project,clip,entry,exit,options with{Protected=[new(0,9,"Immediate reaction")]}),"protected"),"Protected immediate-reaction frames reject conflicting fitting");
        check(Reject(()=>BoundaryFitting.Prepare(project,clip,entry with{SkeletonFingerprint="different"},exit,options),"rig revision"),"References from another native bind/hierarchy revision reject");
        check(Reject(()=>BoundaryFitting.Prepare(project,clip,entry with{Frame=double.NaN},exit,options),"reference frame"),"Nonfinite reference phases reject before curve editing");
        check(Reject(()=>BoundaryFitting.Prepare(project,clip,entry,exit,options with{Bones=["missing"]}),"existing bones"),"Unknown mask bones cannot create or replace native joints");
        check(Reject(()=>BoundaryFitting.Prepare(project,clip,entry,exit,options with{EntrySamples=1}),"two samples"),"A one-sample transition cannot pretend to match endpoint velocity");
        var exitOnly=BoundaryFitting.Prepare(project,clip,null,exit,options);Near(exitOnly.Clip.Tracks[1].Channels[3]!.Sample(0),0,"Exit-only fitting leaves the original entry untouched");
        var windingChannels=referenceChannels.ToArray();windingChannels[3]=new([new(0,380),new(25,380)]);var winding=entry with{Clip=reference with{Tracks=[new("joint",windingChannels)]}};
        var fullTurns=BoundaryFitting.Prepare(project,clip,winding,null,options with{NearestEquivalentAngles=false});Near(fullTurns.Clip.Tracks[1].Channels[3]!.Sample(0),380,"Authored reference turn counts survive when nearest-equivalent angles are disabled");
        var nearest=BoundaryFitting.Prepare(project,clip,winding,null,options);Near(nearest.Clip.Tracks[1].Channels[3]!.Sample(0),20,"Explicit nearest-equivalent policy changes orientation-equivalent turn counts only inside the fit");
        var jumpChannels=referenceChannels.ToArray();jumpChannels[3]=pair;var jump=entry with{Clip=reference with{Tracks=[new("joint",jumpChannels)]},Frozen=false};
        check(Reject(()=>BoundaryFitting.Prepare(project,clip,jump,null,options),"discontinuity"),"Moving reference value discontinuities reject rather than inventing an outgoing pose");
        var contact=new ContactPin(Guid.NewGuid(),"Original lock",0,Vector3.Zero,Vector3.Zero,Quaternion.Identity,true,"Original support");
        check(Reject(()=>BoundaryFitting.Prepare(project with{AuthoringContacts=[contact]},clip,entry,exit,options),"contact/soft-pin"),"Active authored contacts block FK-only fitting instead of being reset after the fit");
        var full=BoundaryFitting.Prepare(project,clip,entry,exit,options with{Bones=["root","joint"],Components=TransformComponents.All});
        check(ReferenceEquals(full.Clip.Tracks[0].Channels[6],root[6]),"Default root policy keeps native root trajectory untouched even in a whole-rig mask");
        var bodySetup=new ControlRigSetup(Guid.NewGuid(),fingerprint,[],[]){BodyBone=1};
        var bodyOnly=BoundaryFitting.Prepare(project with{ControlRigs=[bodySetup]},clip,entry,exit,options with{Bones=["root","joint"],Components=TransformComponents.All,PreserveRootTranslation=false});
        check(ReferenceEquals(bodyOnly.Clip.Tracks[0],clip.Tracks[0])&&bodyOnly.Changed.All(t=>t.Bone!="root"),
            "Reviewed body fitting retains every protected control channel even when the legacy root-translation option is disabled");
        // Same channels under unfamiliar names and a different storage order.
        var renamedScene=scene with{Bones=[scene.Bones[0] with{Name="a"},scene.Bones[1] with{Name="b"}]};
        var renamedClip=clip with{Tracks=[clip.Tracks[1] with{Bone="b"},clip.Tracks[0] with{Bone="a"}]};var renamedRef=reference with{Tracks=[reference.Tracks[0] with{Bone="b"}]};var renamedProject=project with{Scene=renamedScene,Clips=[renamedClip,renamedRef]};
        var renamed=BoundaryFitting.Prepare(renamedProject,renamedClip,new(renamedRef,AnimationControls.Fingerprint(renamedScene),2),new(renamedRef,AnimationControls.Fingerprint(renamedScene),3),options with{Bones=["b"]});
        check(renamed.Clip.Tracks.Single(t=>t.Bone=="b").Channels[3]!.Keys.SequenceEqual(curve.Keys),"One fitting algorithm is independent of bone names and track storage order");
        Directory.CreateDirectory(dir);string path=Path.Combine(dir,"boundary.fighterport");await ProjectStore.SaveAsync(project with{Clips=[fit.Clip]},path);var reopened=await ProjectStore.LoadAsync(path);
        check(reopened.Clips[0].Transition?.CurveFit?.Version==BoundaryFitting.Version&&reopened.Clips[0].Transition?.Start?.Frozen==true,"Project storage retains the transition recipe, exact anchors and bounded FK receipt");
        check(reopened.Clips[0].Tracks[1].Channels[3]!.Keys.SequenceEqual(curve.Keys),"Project reopen retains fitted Hermite keys and duplicate/tangent state exactly");
        await ExitRecoveryAsync(dir,check);
        await BoundaryFloorRecoveryChecks.RunAsync(dir,check);
    }

    static async Task ExitRecoveryAsync(string dir,Action<bool,string> check)
    {
        // Frozen analytic expectations: N26, six-sample exit -> join20/end25.
        // Rx join20,v'=1 to target60,v'=0 gives H(22.5)=40.625.
        // Tx join22,v'=1 to target40,v'=0 gives H(22.5)=31.625.
        // A duplicated join with outgoing30,v'=3 gives H(22.5)=46.875.
        const float tolerance=.00003f;
        var scene=new Scene("Original reviewed recovery rig",[
            new("global",-1,Transform.Identity),
            new("pelvis",0,new(Vector3.One,Vector3.Zero,new(0,5,0))),
            new("upper",1,new(Vector3.One,Vector3.Zero,new(1,-1,0))),
            new("lower",2,new(Vector3.One,Vector3.Zero,new(0,-2,0))),
            new("ankle",3,new(Vector3.One,Vector3.Zero,new(0,-2,0))),
            new("toe",4,new(Vector3.One,Vector3.Zero,new(0,0,1))),
            new("arm",1,new(Vector3.One,Vector3.Zero,new(2,2,0)))],[]);
        string fingerprint=AnimationControls.Fingerprint(scene);
        var setup=new ControlRigSetup(Guid.NewGuid(),fingerprint,[new("Foot.L",2,3,4,Vector3.UnitZ,"Original reviewed leg geometry",true)],[])
            {BodyBone=1,BoneRoles=[new("Hips",1,"Original reviewed body ancestry",true)]};
        Curve?[] SourceChannels(int i)
        {
            var c=new Curve?[9];
            if(i==0)c[6]=new([new(0,9),new(25,99)]);
            else {c[3]=new([new(0,0,1,1),new(25,25,1,1)]);if(i==1)c[6]=new([new(0,2,1,1),new(25,27,1,1)]);}
            return c;
        }
        Curve?[] ReferenceChannels(int i)
        {
            var c=new Curve?[9];if(i==0)c[6]=new([new(0,99),new(25,99)]);
            else {c[3]=new([new(0,60),new(25,60)]);if(i==1)c[6]=new([new(0,40),new(25,40)]);}
            return c;
        }
        var source=new AnimationClip(Guid.NewGuid(),"Original recovery action",26,new(60),false,scene.Bones.Select((bone,i)=>new BoneTrack(bone.Name,SourceChannels(i))).ToArray());
        var reference=source with{Id=Guid.NewGuid(),Revision=Guid.NewGuid(),Name="Authentic first pose",Tracks=scene.Bones.Select((bone,i)=>new BoneTrack(bone.Name,ReferenceChannels(i))).ToArray()};
        var project=new FighterProject(1,Guid.NewGuid(),"Recovery",scene,[source,reference]){ControlRigs=[setup]};
        var settings=new BoundaryFitSettings(0,6,TransformComponents.All,scene.Bones.Select(b=>b.Name).ToArray(),[new(0,20,"Authored action prefix")])
            {PreserveContacts=true,ContactBones=["toe"],ExitContactRecovery=true};
        var anchor=new BoundaryAnchor(reference,fingerprint,0,true);
        string before=JsonSerializer.Serialize(project,ProjectStore.Json);
        var fit=BoundaryFitting.Prepare(project,source,null,anchor,settings);
        var hip=fit.Clip.Tracks.Single(t=>t.Bone=="pelvis");
        check(fit.Receipt.Version==BoundaryFitting.ExitRecoveryVersion&&fit.Receipt.Settings.ExitContactRecovery&&
            fit.Receipt.RecoveryStart==20&&fit.Receipt.RecoveryEnd==25&&fit.Receipt.RecoveredContactBones.SequenceEqual(new[]{"pelvis","upper","lower","ankle","toe"})&&fit.Receipt.ProtectedContactBones.SequenceEqual(new[]{"global"}),
            "Reviewed exit recovery records exact released hip/leg paths, retained global path and bounded final span");
        check(Math.Abs(hip.Channels[3]!.Sample(22.5)-40.625f)<tolerance&&Math.Abs(hip.Channels[6]!.Sample(22.5)-31.625f)<tolerance,
            "Exit recovery midpoint matches independent frozen Hermite rotation and translation polynomials");
        bool prefix=true;
        for(int i=0;i<=640;i++)
        {
            double frame=i/32d;var a=AnimationService.Evaluate(scene,source,frame);var b=AnimationService.Evaluate(scene,fit.Clip,frame);
            prefix&=a.Local.Zip(b.Local).All(pair=>pair.First.Channels().Zip(pair.Second.Channels()).All(x=>Math.Abs(x.First-x.Second)<tolerance));
            prefix&=a.World.Zip(b.World).All(pair=>MatrixNear(pair.First,pair.Second,tolerance));
        }
        check(prefix,"Exit recovery preserves the complete original local/world action through the join at independent thirty-second-frame samples");
        var endpoint=AnimationService.Evaluate(scene,fit.Clip,25);var expected=AnimationService.Evaluate(scene,reference,0);
        check(endpoint.Local.Zip(expected.Local).All(pair=>pair.First.Channels().Zip(pair.Second.Channels()).All(x=>Math.Abs(x.First-x.Second)<tolerance))&&
            endpoint.World.Zip(expected.World).All(pair=>MatrixNear(pair.First,pair.Second,tolerance)),
            "Complete reviewed body and free arm branch reach the authentic first pose in both local and world transforms");
        check(fit.Changed.All(t=>t.Bone!="global")&&ReferenceEquals(fit.Clip.Tracks[0],source.Tracks[0])&&source.Tracks[0].Channels.SequenceEqual(fit.Clip.Tracks[0].Channels),
            "Exit recovery never rewrites source-authored protected motion-control channels");
        check(fit.Changed.SelectMany(t=>t.Channels.OfType<Curve>()).All(c=>c.Keys.Last(k=>k.Frame==25).InTangent==0&&c.Keys.Last(k=>k.Frame==25).OutTangent==0),
            "Every fitted recovery endpoint has explicit zero incoming and outgoing slope");
        check(JsonSerializer.Serialize(project,ProjectStore.Json)==before&&fit.Clip.Id!=source.Id&&fit.Clip.Revision!=source.Revision,
            "Exit recovery prepares an independent revision while keeping complete source scene, clips and constraints immutable");
        var legacy=BoundaryFitting.Prepare(project,source,null,anchor,settings with{ExitContactRecovery=false});
        check(legacy.Receipt.Version==BoundaryFitting.ContactVersion&&ReferenceEquals(legacy.Clip.Tracks[1],source.Tracks[1])&&legacy.Receipt.RecoveredContactBones.Length==0,
            "Whole-clip contact path preservation remains the unchanged default unless explicit recovery is selected");
        var pairChannels=source.Tracks[1].Channels.ToArray();pairChannels[3]=new([new(0,0,1,1),new(20,20,2,2),new(20,30,3,3),new(25,35,1,1)]);
        var paired=source with{Tracks=source.Tracks.Select(t=>t.Bone=="pelvis"?t with{Channels=pairChannels}:t).ToArray()};
        var pairedFit=BoundaryFitting.Prepare(project,paired,null,anchor,settings);var pairResult=pairedFit.Clip.Tracks[1].Channels[3]!;
        check(pairResult.Keys.Where(k=>k.Frame<=20).SequenceEqual(pairChannels[3]!.Keys.Where(k=>k.Frame<=20))&&
            BoundaryFitting.Derivative(pairResult,20,false)==2&&BoundaryFitting.Derivative(pairResult,20)==3&&Math.Abs(pairResult.Sample(22.5)-46.875f)<tolerance,
            "Recovery retains both exact native join key sides and incoming/outgoing derivatives rather than normalizing authored discontinuities");
        bool Reject(FighterProject p,BoundaryFitSettings s,BoundaryAnchor? e=null,BoundaryAnchor? x=null,string text="")
        {try{BoundaryFitting.Prepare(p,source,e,x??anchor,s);return false;}catch(Exception error)when(error is InvalidDataException or NotSupportedException){return error.Message.Contains(text,StringComparison.OrdinalIgnoreCase);}}
        check(Reject(project,settings with{Bones=["pelvis","upper","lower","ankle","arm"]},text:"complete"),"Recovery rejects an incomplete foot/toe dependency mask instead of leaving an endpoint jump");
        check(Reject(project,settings,e:anchor,text:"exit-only")&&Reject(project,settings,x:anchor with{Frozen=false},text:"frozen first-frame")&&Reject(project,settings,x:anchor with{Frame=1},text:"first-frame"),
            "Recovery rejects entry fitting, moving references and non-first reference frames");
        check(Reject(project,settings with{Components=TransformComponents.Rotation},text:"all S/R/T")&&Reject(project,settings with{Protected=[new(22,24,"Fixed action interval")]},text:"protected"),
            "Recovery rejects partial pose masks and final windows intersecting protected action intervals");
        check(Reject(project with{ControlRigs=[setup with{BoneRoles=[]}]},settings,text:"reviewed")&&
            Reject(project with{ControlRigs=[setup with{Chains=setup.Chains.Select(c=>c with{Reviewed=false}).ToArray()}]},settings,text:"reviewed"),
            "Recovery requires reviewed body and leg evidence rather than bone names or inferred anatomy");
        check(Reject(project,settings with{ContactBones=["toe","arm"]},text:"hip/leg"),"Hand or other unsupported support dependencies reject the bounded leg-recovery policy");
        var pin=new ContactPin(Guid.NewGuid(),"Original fixed support",5,Vector3.Zero,Vector3.Zero,Quaternion.Identity,true,"Original anchor");
        check(Reject(project with{AuthoringContacts=[pin]},settings,text:"pin")&&Reject(project with{AuthoringContacts=[pin with{SoftSupport=true,EditorBehavior=EditorPinBehavior.SoftReach}]},settings,text:"pin"),
            "Authored fixed and soft pins intersecting recovery reject without releasing or deleting anchors");
        var contact=new ContactFrame(Guid.NewGuid(),source.Id,0,fingerprint,[pin]){EndFrame=20};
        var constrained=project with{Contacts=[contact]};
        var allowed=BoundaryFitting.Prepare(constrained,source,null,anchor,settings);
        check(ReferenceEquals(constrained.Contacts[0],contact)&&allowed.Receipt.RecoveryStart==20,
            "A contact interval ending exactly at the unchanged join remains intact and permits subsequent explicit recovery");
        check(Reject(project with{Contacts=[contact with{Frame=22,EndFrame=25}]},settings,text:"interval")&&
            Reject(project with{BonePins=[new(Guid.NewGuid(),5,source.Id,fingerprint)]},settings,text:"editor pin"),
            "Overlapping timed contacts and existing-bone editor pins fail closed without stripped metadata");
        foreach(int component in new[]{0,3,6})
        {
            var contradictory=reference.Tracks[0].Channels.ToArray();contradictory[component]=new([new(0,component==0?1.001f:component==3?.1f:98.99f)]);
            var other=reference with{Tracks=reference.Tracks.Select(t=>t.Bone=="global"?t with{Channels=contradictory}:t).ToArray()};
            var placed=BoundaryFitting.Prepare(project,source,null,anchor with{Clip=other},settings);
            check(ReferenceEquals(placed.Clip.Tracks[0],source.Tracks[0])&&placed.Receipt.ReferenceRootPolicy==BoundaryFitting.SourceRootReferencePolicy&&
                placed.Clip.Tracks.Skip(1).SelectMany(t=>t.Channels).Zip(fit.Clip.Tracks.Skip(1).SelectMany(t=>t.Channels)).All(pair=>pair.First is null?pair.Second is null:pair.Second is not null&&pair.First.Keys.SequenceEqual(pair.Second.Keys)),
                "Recovery separates authentic body pose from differing reference root "+(component==0?"scale":component==3?"orientation":"translation")+" and retains exact source-authored controls");
        }
        var equivalentRoot=reference.Tracks[0].Channels.ToArray();equivalentRoot[3]=new([new(0,360)]);
        var equivalent=reference with{Tracks=reference.Tracks.Select(t=>t.Bone=="global"?t with{Channels=equivalentRoot}:t).ToArray()};
        var equivalentFit=BoundaryFitting.Prepare(project,source,null,anchor with{Clip=equivalent},settings);
        check(ReferenceEquals(equivalentFit.Clip.Tracks[0],source.Tracks[0]),"Full-turn reference roots never replace or realign original source root channels");
        var loopSource=source with{Loop=true,Tracks=source.Tracks.Select(t=>t.Bone=="pelvis"?t with{Channels=new Curve?[9]}:t with{Channels=t.Channels.Select(c=>c is null?null:c with{LoopFrameLimit=27}).ToArray()}).ToArray()};
        string loopOriginal=JsonSerializer.Serialize(loopSource,ProjectStore.Json);bool loopRejected=false;
        try{BoundaryFitting.Prepare(project with{Clips=[loopSource,reference]},loopSource,null,anchor,settings);}catch(NotSupportedException error){loopRejected=error.Message.Contains("non-looping",StringComparison.Ordinal);}
        // A naive join/end-only channel would bridge target60 back toward
        // absent/bind0 over span22: at time0, u=2/22 and H=58.602554...
        var unsafeWrap=new Curve([new(20,0),new(25,60)],Interpolation.Hermite,27);
        double u=2d/22,wrapExpected=60*(2*u*u*u-3*u*u+1);
        check(Math.Abs(unsafeWrap.Sample(0)-wrapExpected)<tolerance&&loopSource.Sample(scene.Bones[1],0).Rotation.X==0&&loopRejected&&JsonSerializer.Serialize(loopSource,ProjectStore.Json)==loopOriginal,
            "Stored-loop recovery rejects absent-channel wrap changes before touching the exact original curves or loop flag");
        var names=scene.Bones.Select((_,i)=>"renamed_"+(97-i)).ToArray();
        var renamedScene=scene with{Bones=scene.Bones.Select((bone,i)=>bone with{Name=names[i]}).ToArray()};string renamedFingerprint=AnimationControls.Fingerprint(renamedScene);
        BoneTrack Rename(BoneTrack t)=>t with{Bone=names[Array.FindIndex(scene.Bones,b=>b.Name==t.Bone)]};
        var renamedSource=source with{Tracks=source.Tracks.Reverse().Select(Rename).ToArray()};var renamedReference=reference with{Tracks=reference.Tracks.Reverse().Select(Rename).ToArray()};
        var renamedProject=project with{Scene=renamedScene,Clips=[renamedSource,renamedReference],ControlRigs=[setup with{SkeletonFingerprint=renamedFingerprint}]};
        var renamedFit=BoundaryFitting.Prepare(renamedProject,renamedSource,null,new(renamedReference,renamedFingerprint,0),settings with{Bones=names.Reverse().ToArray(),ContactBones=[names[5]]});
        check(fit.Clip.Tracks.All(t=>t.Channels.Zip(renamedFit.Clip.Tracks.Single(other=>other.Bone==names[Array.FindIndex(scene.Bones,b=>b.Name==t.Bone)]).Channels)
            .All(pair=>pair.First is null?pair.Second is null:pair.Second is not null&&pair.First.Keys.SequenceEqual(pair.Second.Keys))),
            "The same exit recovery algorithm is invariant to unfamiliar bone names and reversed source/mask storage order");
        string path=Path.Combine(dir,"exit-recovery.fighterport");await ProjectStore.SaveAsync(project with{Clips=[fit.Clip,reference]},path);var reopened=await ProjectStore.LoadAsync(path);
        var receipt=reopened.Clips[0].Transition!.CurveFit!;
        check(receipt.Version==BoundaryFitting.ExitRecoveryVersion&&receipt.Settings.ExitContactRecovery&&receipt.RecoveryStart==20&&receipt.RecoveryEnd==25&&receipt.RecoveredContactBones.SequenceEqual(fit.Receipt.RecoveredContactBones)&&
            reopened.Clips[0].Tracks.SelectMany(t=>t.Channels).Zip(fit.Clip.Tracks.SelectMany(t=>t.Channels)).All(pair=>pair.First is null?pair.Second is null:pair.Second is not null&&pair.First.Keys.SequenceEqual(pair.Second.Keys)),
            "Project reopen preserves the explicit recovery permission, exact released paths/span and all fitted curve keys/tangents");
    }
    static bool MatrixNear(Matrix4x4 a,Matrix4x4 b,float epsilon)=>
        new[]{a.M11-b.M11,a.M12-b.M12,a.M13-b.M13,a.M14-b.M14,a.M21-b.M21,a.M22-b.M22,a.M23-b.M23,a.M24-b.M24,
            a.M31-b.M31,a.M32-b.M32,a.M33-b.M33,a.M34-b.M34,a.M41-b.M41,a.M42-b.M42,a.M43-b.M43,a.M44-b.M44}.All(value=>Math.Abs(value)<epsilon);
}
