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
        // Same channels under unfamiliar names and a different storage order.
        var renamedScene=scene with{Bones=[scene.Bones[0] with{Name="a"},scene.Bones[1] with{Name="b"}]};
        var renamedClip=clip with{Tracks=[clip.Tracks[1] with{Bone="b"},clip.Tracks[0] with{Bone="a"}]};var renamedRef=reference with{Tracks=[reference.Tracks[0] with{Bone="b"}]};var renamedProject=project with{Scene=renamedScene,Clips=[renamedClip,renamedRef]};
        var renamed=BoundaryFitting.Prepare(renamedProject,renamedClip,new(renamedRef,AnimationControls.Fingerprint(renamedScene),2),new(renamedRef,AnimationControls.Fingerprint(renamedScene),3),options with{Bones=["b"]});
        check(renamed.Clip.Tracks.Single(t=>t.Bone=="b").Channels[3]!.Keys.SequenceEqual(curve.Keys),"One fitting algorithm is independent of bone names and track storage order");
        Directory.CreateDirectory(dir);string path=Path.Combine(dir,"boundary.fighterport");await ProjectStore.SaveAsync(project with{Clips=[fit.Clip]},path);var reopened=await ProjectStore.LoadAsync(path);
        check(reopened.Clips[0].Transition?.CurveFit?.Version==BoundaryFitting.Version&&reopened.Clips[0].Transition?.Start?.Frozen==true,"Project storage retains the transition recipe, exact anchors and bounded FK receipt");
        check(reopened.Clips[0].Tracks[1].Channels[3]!.Keys.SequenceEqual(curve.Keys),"Project reopen retains fitted Hermite keys and duplicate/tangent state exactly");
    }
}
