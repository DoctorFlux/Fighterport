using FighterPort.Core;

internal static class SmallFileOptimizationChecks
{
    public static void Run(Action<bool,string> check)
    {
        Defaults(check);
        Bounds(check);
        FitAndAuthoredProtections(check);
        NativeStructure(check);
        LimitsAndIdentities(check);
    }

    static AnimationOptimizationSettings Small(double length=1)=>new(){Mode=AnimationOptimizationMode.SmallFile,BindBoundsDiagonal=length};
    static AnimationClip Clip(string bone,int channel,Curve curve)
    {
        var channels=new Curve?[9];channels[channel]=curve;
        return new(Guid.NewGuid(),"Independent small-file curve",5,new(60),false,[new(bone,channels)]);
    }
    static Curve Pulse(float value)=>new([new(0,0),new(2,value),new(4,0)]);
    static Curve Output(AnimationOptimizationResult result,int channel)=>result.Tracks.Single().Channels[channel]!;
    static AnimationOptimizationCertificate Certify(AnimationClip source,Curve curve,int channel,AnimationOptimizationSettings settings)=>
        AnimationOptimization.CertifyCandidate(source,[source.Tracks[0] with{Channels=source.Tracks[0].Channels.Select((original,index)=>index==channel?curve:original).ToArray()}],settings,targetBoneNames:[source.Tracks[0].Bone]);

    static void Defaults(Action<bool,string> check)
    {
        var exact=new AnimationOptimizationSettings();var quality=exact with{Mode=AnimationOptimizationMode.HighQuality};var small=Small();
        check((int)AnimationOptimizationMode.Exact==0&&(int)AnimationOptimizationMode.HighQuality==1&&(int)AnimationOptimizationMode.SmallFile==2,
            "Small file is an additive quality profile; existing Exact/High quality enum values remain unchanged");
        check(exact.Mode==AnimationOptimizationMode.Exact&&quality.EulerValueBoundDegrees==.01&&quality.EulerSpeedBoundDegreesPerFrame==.05&&
            quality.ScaleValueBound==1e-5&&quality.TranslationAbsoluteCap==.0002,
            "Existing default Exact and High quality scalar settings are unchanged");
        check(small.EulerValueBoundDegrees==.1&&small.EulerSpeedBoundDegreesPerFrame==.1&&small.ScaleValueBound==.0001&&
            small.ScaleSpeedBoundPerFrame==.0001&&small.TranslationAbsoluteCap==.002&&small.TranslationSpeedAbsoluteCapPerFrame==.002,
            "Explicit Small file freezes rotation, translation, scale and per-frame derivative ceilings");
        check(small.MaximumWork==2_000_000&&small.MaximumCandidateSpans==100_000,
            "Small file retains the original proof-work and candidate-span budgets");
    }

    static void Bounds(Action<bool,string> check)
    {
        foreach(var item in new[]{(Channel:3,Value:.025f,Bound:.1),(Channel:6,Value:.001f,Bound:.002),(Channel:0,Value:.00005f,Bound:.0001)})
        {
            var curve=item.Channel==0?new Curve([new(0,1),new(2,1+item.Value),new(4,1)]):Pulse(item.Value);
            var source=Clip("unfamiliar joint",item.Channel,curve);
            var small=AnimationOptimization.Optimize(source,["unfamiliar joint"],Small());
            var quality=AnimationOptimization.Optimize(source,["unfamiliar joint"],new(){Mode=AnimationOptimizationMode.HighQuality,BindBoundsDiagonal=1});
            check(Output(small,item.Channel).Keys.Length==2&&Output(quality,item.Channel).Keys.Length==3,
                "Small file reduces an independently authored pulse rejected by unchanged High quality / channel "+item.Channel);
            var certificate=Certify(source,Output(small,item.Channel),item.Channel,Small());
            check(certificate.Accepted&&certificate.Channels.Single().ValueBound==item.Bound&&certificate.MaximumValueBound>0,
                "The complete reduced pulse independently certifies against the frozen original and explicit profile bound / channel "+item.Channel);
        }
        foreach(double length in new[]{.01,100d})
        {
            var source=Clip("arbitrary length reference",6,Pulse(.001f));
            var result=AnimationOptimization.Optimize(source,["arbitrary length reference"],Small(length));
            check(result.Report.Channels.Single().ValueBound==.002&&Output(result,6).Keys.Length==2&&
                result.Report.Claim.Contains("cumulative orientation/world/skin"),
                "Small translation construction uses native .002 units while collective geometry remains a separate adapter gate / L="+length);
        }
        var derivative=Clip("rapid independent pulse",3,new([new(0,0),new(1,.09f),new(2,0)])) with{SampleCount=3};
        var retained=AnimationOptimization.Optimize(derivative,["rapid independent pulse"],Small());
        check(Output(retained,3).Keys.Length==3,
            "A pulse inside the Small rotation value bound retains its knot when its independently known .135-degree/frame derivative exceeds .1");
        var tooLarge=Clip("outside scalar ceiling",3,Pulse(.125f));
        check(Output(AnimationOptimization.Optimize(tooLarge,["outside scalar ceiling"],Small()),3).Keys.Length==3,
            "Small file cannot flatten an authored rotation outside its .1-degree ceiling");
    }

    static void FitAndAuthoredProtections(Action<bool,string> check)
    {
        var source=Clip("reviewed body identity",3,Pulse(.025f));
        var changed=new Curve([new(0,0),new(2,.005f,.01f,.01f),new(4,0)]);
        var small=Small() with{FitJoinFrames=[2]};
        var strict=new AnimationOptimizationSettings{Mode=AnimationOptimizationMode.HighQuality,BindBoundsDiagonal=1,FitJoinFrames=[2]};
        var reduced=AnimationOptimization.Optimize(source,["reviewed body identity"],small);
        check(Output(reduced,3).Keys.Any(k=>k.Frame==2),
            "Small curve reduction keeps an existing fitting join's native knot time");
        check(Certify(source,changed,3,small).Accepted&&!Certify(source,changed,3,strict).Accepted,
            "Small decoded fitting joins may change values/tangents within their bounds; High quality fitting joins remain exact");
        check(!Certify(source,new([new(0,0),new(4,0)]),3,small).Accepted,
            "Small decoded fitting cannot drop the authored join time/count while approximating its numeric values");
        check(!Certify(source,changed,3,small with{MandatoryFrames=[2]}).Accepted,
            "Explicit mandatory frames stay exact in Small file even when they coincide with fitting joins");
        check(!Certify(source,changed,3,small with{ProtectedRanges=[new(2,2,"Authored fixed contact")]}).Accepted,
            "An authored contact point cannot be loosened by a coincident Small fitting join");
        check(!Certify(source,changed,3,small with{PreserveStructureBones=["reviewed body identity"]}).Accepted,
            "Pinned structural paths retain exact tuples in Small file");
        check(!Certify(source,changed,3,small with{ExactOnlyBones=["reviewed body identity"]}).Accepted,
            "Reviewed source-owned motion retains exact functions in Small file");
        check(!Certify(source,changed,3,small with{ChannelRules=[new("reviewed body identity",3){MandatoryFrames=[2]}]}).Accepted,
            "Explicit channel-specific mandatory points remain exact in Small file");
        var constant=Clip("constant fitting function",3,new([new(0,7),new(2,7),new(4,7)]));
        var constantResult=AnimationOptimization.Optimize(constant,["constant fitting function"],small);
        check(Output(constantResult,3).Keys.Length==1&&Certify(constant,Output(constantResult,3),3,small).Accepted,
            "A proved constant still permits native fixed storage without inventing motion at a fitting join");
    }

    static void NativeStructure(Action<bool,string> check)
    {
        var pair=Clip("paired identities",3,new([new(0,0),new(2,.01f),new(2,.02f),new(4,.02f)]));
        var paired=AnimationOptimization.Optimize(pair,["paired identities"],Small());
        check(Output(paired,3).Keys.Count(k=>k.Frame==2)==2,
            "Small file preserves both authored paired-key sides");
        float negativeZero=BitConverter.Int32BitsToSingle(int.MinValue);
        var signed=Clip("signed identities",3,new([new(0,0),new(2,negativeZero),new(4,0)]));
        var signedResult=AnimationOptimization.Optimize(signed,["signed identities"],Small());
        check(Output(signedResult,3).Keys.Any(k=>BitConverter.SingleToInt32Bits(k.Value)==int.MinValue),
            "Small file preserves authored negative zero rather than normalizing it");
        var singular=Clip("singular mirrored identity",0,new([new(0,1),new(2,0),new(4,-1)]));
        check(Output(AnimationOptimization.Optimize(singular,["singular mirrored identity"],Small()),0).Keys.Length==3,
            "Singular and sign-changing scale stays exact-only under Small file");
        var turn=Clip("authored winding",5,new([new(0,0,180,180),new(2,360,180,180),new(4,720,180,180)]));
        var turns=AnimationOptimization.Optimize(turn,["authored winding"],Small());
        check(Output(turns,5).Keys.Last().Value==720&&Math.Abs(Output(turns,5).Sample(1.5)-270)<1e-5,
            "Small file retains a full authored 720-degree Euler path without quaternion-shortest-path conversion");
        check(turn.SampleCount==5&&turn.Rate==new RationalRate(60)&&!turn.Loop,
            "Small optimization keeps native timing, rate and loop metadata outside the curve reducer unchanged");
    }

    static void LimitsAndIdentities(Action<bool,string> check)
    {
        var source=Clip("numeric identity",3,Pulse(.025f));
        foreach(var settings in new[]{Small(0),Small() with{EulerValueBoundDegrees=.101},Small() with{MaximumWork=2_000_001},Small() with{MaximumCandidateSpans=100_001}})
        {
            bool rejected=false;try{AnimationOptimization.Optimize(source,["numeric identity"],settings);}catch(ArgumentException){rejected=true;}
            check(rejected,"Small file rejects missing scale, out-of-profile bounds or expanded operation budgets");
        }
        var limited=AnimationOptimization.Optimize(source,["numeric identity"],Small() with{MaximumWork=0});
        check(ReferenceEquals(Output(limited,3),source.Tracks[0].Channels[3])&&limited.Report.WorkLimitReached,
            "Small proof exhaustion retains unchanged source curves instead of accepting an unproved approximation");
        using var cancellation=new CancellationTokenSource();cancellation.Cancel();bool cancelled=false;
        try{AnimationOptimization.Optimize(source,["numeric identity"],Small(),cancellation.Token);}catch(OperationCanceledException){cancelled=true;}
        check(cancelled,"Small file obeys cancellation before curve publication");
        var renamed=source with{Tracks=[source.Tracks[0] with{Bone="anonymous-permuted-joint"}]};
        var first=AnimationOptimization.Optimize(source,["numeric identity"],Small());
        var other=AnimationOptimization.Optimize(renamed,["anonymous-permuted-joint"],Small());
        check(Output(first,3).Keys.SequenceEqual(Output(other,3).Keys)&&source.Tracks[0].Channels[3]!.Keys.Length==3,
            "Small construction depends on authored numbers and declared protections, not character, action or bone-name exceptions");
    }
}
