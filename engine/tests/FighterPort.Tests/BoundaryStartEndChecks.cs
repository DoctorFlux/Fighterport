using FighterPort.Core;
using System.Numerics;
using System.Text.Json;

internal static class BoundaryStartEndChecks
{
    public static async Task RunAsync(string dir,Action<bool,string> check)
    {
        // Frozen before tuning: 40 frames; entry native times 0..4, exit
        // 25..39. Original linear Rx=t; frozen reference Rx=60. Independent
        // cubic midpoint expectations: entry H(2)=31.5, exit H(32)=44.25.
        var scene=new Scene("Unfamiliar two-ended fixture",[
            new("travel",-1,Transform.Identity),new("orientation",0,Transform.Identity),
            new("body",1,new(Vector3.One,Vector3.Zero,new(0,5,0))),
            new("upper",2,new(Vector3.One,Vector3.Zero,new(1,-1,0))),
            new("lower",3,new(Vector3.One,Vector3.Zero,new(0,-2,0))),
            new("foot",4,new(Vector3.One,Vector3.Zero,new(0,-2,0))),
            new("toe",5,new(Vector3.One,Vector3.Zero,new(0,0,1)))],[]);
        var fp=AnimationControls.Fingerprint(scene);
        var setup=new ControlRigSetup(Guid.NewGuid(),fp,[new("Foot.L",3,4,5,Vector3.UnitZ,"Reviewed authentic hierarchy",true)],[])
        {BodyBone=2,BoneRoles=[new("RootMotion",0,"Reviewed actor placement",true),new("BodyOrientation",1,"Reviewed ancestor",true),new("Hips",2,"Reviewed body",true)]};
        Curve?[] Channels(int bone,bool reference)
        {
            var c=new Curve?[9];
            if(bone==0)c[6]=new([new(0,reference?900:9,1,1),new(39,reference?900:48,1,1)]);
            else c[3]=reference?new([new(0,60),new(39,60)]):new([new(0,0,1,1),new(39,39,1,1)]);
            return c;
        }
        var source=new AnimationClip(Guid.NewGuid(),"40 native samples",40,new(60),false,scene.Bones.Select((b,i)=>new BoneTrack(b.Name,Channels(i,false))).ToArray());
        var reference=source with{Id=Guid.NewGuid(),Revision=Guid.NewGuid(),Name="First target pose",Tracks=scene.Bones.Select((b,i)=>new BoneTrack(b.Name,Channels(i,true))).ToArray()};
        var project=new FighterProject(1,Guid.NewGuid(),"Start and end",scene,[source,reference]){ControlRigs=[setup]};
        var settings=new BoundaryFitSettings(5,15,TransformComponents.All,scene.Bones.Select(b=>b.Name).ToArray(),[new(4,25,"Original action middle")])
        {MatchReferenceControls=true,PreserveContacts=true,ContactBones=["toe"],BoundaryContactRecovery=true};
        var anchor=new BoundaryAnchor(reference,fp,0);string original=JsonSerializer.Serialize(project,ProjectStore.Json);
        var result=BoundaryFitting.Prepare(project,source,anchor,anchor,settings);
        var curve=result.Clip.Tracks.Single(t=>t.Bone=="body").Channels[3]!;
        check(Math.Abs(curve.Sample(2)-31.5)<.00003&&Math.Abs(curve.Sample(32)-44.25)<.00003,"Both recovery windows match independent fixed-duration Hermite expectations 31.5 / 44.25");
        check(result.Receipt.Version==BoundaryFitting.BoundaryRecoveryVersion&&result.Receipt.RecoveryWindows.Select(w=>(w.Side,w.Start,w.End)).SequenceEqual(new[]{("entry",0,4),("exit",25,39)}),"40-frame receipt records entry 1..5 and final 15 inclusive samples 26..40 with shared original joins");
        check(result.Clip.SampleCount==40&&result.Clip.Rate==source.Rate&&!result.Clip.Loop&&ReferenceEquals(result.Clip.Tracks[0],source.Tracks[0]),"Start/end fitting retains exact duration, timebase and complete moving motion-position track");
        check(Enumerable.Range(0,1345).All(i=>Math.Abs(curve.Sample(4+i/64d)-(4+i/64d))<.00003),"Complete unchanged attack middle retains the original curve at independent fractional witnesses");
        check(result.Clip.Tracks.Skip(1).All(t=>t.Channels[3]!.Sample(0)==60&&t.Channels[3]!.Sample(39)==60&&BoundaryFitting.Derivative(t.Channels[3],0)==0&&BoundaryFitting.Derivative(t.Channels[3],39,false)==0),"Both authentic first-pose endpoints have zero slope while reviewed ancestor orientation fits with the body");
        check(JsonSerializer.Serialize(project,ProjectStore.Json)==original,"Two-ended preparation preserves the complete input document and baked support metadata");
        var paired=source with{Tracks=source.Tracks.Select(t=>t.Bone!="body"?t:t with{Channels=t.Channels.Select((c,i)=>i!=3?c:new Curve([new(0,0,1,1),new(4,4,2,2),new(4,8,3,3),new(25,25,4,4),new(25,30,5,5),new(39,39,1,1)])).ToArray()}).ToArray()};
        var pairFit=BoundaryFitting.Prepare(project,paired,anchor,anchor,settings).Clip.Tracks.Single(t=>t.Bone=="body").Channels[3]!;
        check(pairFit.Keys.Where(k=>k.Frame>=4&&k.Frame<=25).SequenceEqual(paired.Tracks.Single(t=>t.Bone=="body").Channels[3]!.Keys.Where(k=>k.Frame>=4&&k.Frame<=25)),"Both shared joins preserve paired native key values and incoming/outgoing tangents exactly");
        var pin=new ContactPin(Guid.NewGuid(),"Fixed original support",6,Vector3.Zero,Vector3.Zero,Quaternion.Identity,true,"Original anchor");
        var middle=new ContactFrame(Guid.NewGuid(),source.Id,4,fp,[pin]){EndFrame=25};
        check(BoundaryFitting.Prepare(project with{Contacts=[middle]},source,anchor,anchor,settings).Receipt.RecoveryWindows.Length==2,"A timed contact contained in the exact protected middle retains its anchor and permits both exterior windows");
        bool Reject(FighterProject p,BoundaryFitSettings s,BoundaryAnchor? e,BoundaryAnchor? x)
        {try{BoundaryFitting.Prepare(p,source,e,x,s);return false;}catch(Exception error)when(error is InvalidDataException or NotSupportedException){return true;}}
        check(Reject(project with{Contacts=[middle with{Frame=3}]},settings,anchor,anchor)&&Reject(project with{Contacts=[middle with{EndFrame=26}]},settings,anchor,anchor),"Timed contacts crossing either boundary reject without dropping pins");
        check(Reject(project with{AuthoringContacts=[pin]},settings,anchor,anchor)&&Reject(project with{BonePins=[new(Guid.NewGuid(),6,source.Id,fp)]},settings,anchor,anchor),"Fixed authored and existing-bone pins remain hard contradictions for both-end recovery");
        check(Reject(project,settings with{EntrySamples=27},anchor,anchor)&&Reject(project,settings with{ExitContactRecovery=true},anchor,anchor)&&Reject(project,settings,anchor with{Frame=1},anchor)&&Reject(project,settings,anchor,anchor with{Frozen=false}),"Overlaps, contradictory legacy mode and moving/non-first recovery anchors reject");
        var entryOnly=BoundaryFitting.Prepare(project,source,anchor,null,settings with{Protected=[],ExitSamples=0});
        var exitOnly=BoundaryFitting.Prepare(project,source,null,anchor,settings with{Protected=[],EntrySamples=0});
        check(entryOnly.Receipt.RecoveryWindows.Single().Side=="entry"&&entryOnly.Clip.Tracks.Single(t=>t.Bone=="body").Channels[3]!.Sample(39)==39&&exitOnly.Receipt.RecoveryWindows.Single().Side=="exit"&&exitOnly.Clip.Tracks.Single(t=>t.Bone=="body").Channels[3]!.Sample(0)==0,"The same explicit adapter supports start only and end only without modifying the opposite boundary");
        string save=Path.Combine(dir,"two-ended-project.json");await ProjectStore.SaveAsync(project with{Clips=[source,reference,result.Clip]},save);
        var reopened=await ProjectStore.LoadAsync(save);check(reopened.Clips.Single(c=>c.Id==result.Clip.Id).Transition!.CurveFit!.RecoveryWindows.Length==2,"Both-window receipt survives the existing project storage contract without a format migration");
    }
}
