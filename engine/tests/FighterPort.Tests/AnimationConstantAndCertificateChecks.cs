using FighterPort.Core;

internal static class AnimationConstantAndCertificateChecks
{
    public static void Run(Action<bool,string> check)
    {
        float[] bind = [1, 1, 1, 0, 0, 0, 3, -2, 0];
        Curve Constant(float value) => new([new(2, value), new(4, value), new(7, value)]);
        BoneTrack Track(string name, params (int Channel, Curve Curve)[] curves)
        {
            var channels = new Curve?[9]; foreach (var item in curves) channels[item.Channel] = item.Curve;
            return new(name, channels);
        }
        AnimationClip Clip(params BoneTrack[] tracks) => new(Guid.NewGuid(), "constant/certificate", 9, new(60,1), false, tracks);
        var settings = new AnimationOptimizationSettings { BindPoses = [new("any identity", bind)] };
        var original = Clip(new BoneTrack("any identity", bind.Select(value => (Curve?)Constant(value)).ToArray()));
        var result = AnimationOptimization.Optimize(original, ["any identity"], settings);
        check(result.Tracks.Length == 0 && result.RemovedBindTracks.SequenceEqual(["any identity"]) && original.Tracks[0].Channels[0]!.Keys.Length == 3,
            "Every exact raw bind-constant channel permits removing the animation track without mutating the model or source");
        foreach (var channel in Enumerable.Range(0,9))
        {
            var sparse = Clip(Track("any identity", (channel, Constant(bind[channel]))));
            check(AnimationOptimization.Optimize(sparse,["any identity"],settings).Tracks.Length == 0,
                "Bind cleanup uses authentic sparse fallback for each native SRT channel / " + channel);
        }
        var spin = Clip(Track("any identity", (3, Constant(360))));
        check(AnimationOptimization.Optimize(spin,["any identity"],settings).Tracks.Single().Channels[3]!.Keys.Single().Value == 360,
            "A constant full Euler turn remains authored instead of being normalized to model zero");
        var moving = Clip(Track("any identity", (6, new([new(0,3),new(4,3,1,1),new(8,3)]))));
        check(AnimationOptimization.Optimize(moving,["any identity"],settings).Tracks.Single().Channels[6]!.Keys.Length == 3,
            "Equal keyed values with nonzero Hermite tangents retain their real between-key movement");
        var negativeZero = BitConverter.Int32BitsToSingle(int.MinValue);
        var signed = Clip(Track("any identity", (4, Constant(negativeZero))));
        check(AnimationOptimization.Optimize(signed,["any identity"],settings).Tracks.Single().Channels[4]!.Keys.Length == 3,
            "Signed zero is retained as authored data rather than discarded as bind zero");
        foreach (var protectedSettings in new[] {
            settings with{MandatoryFrames=[4]}, settings with{PreserveStructureBones=["any identity"]},
            settings with{ProtectedRanges=[new(2,7,"authored protection")]},
            settings with{ChannelRules=[new("any identity",6){MandatoryFrames=[4]}]}})
            check(AnimationOptimization.Optimize(original,["any identity"],protectedSettings).Tracks.Length == 1,
                "Fitting joins and authored structural/contact protections prevent bind-track removal");
        var shifted = Clip(Track("any identity",(7,new([new(4,-3)]))));
        check(AnimationOptimization.Optimize(shifted,["any identity"],settings).Tracks.Single().Channels[7]!.Keys.Single().Frame == 0,
            "Even a single shifted constant key is represented by the native fixed first-frame key");

        var quality = new AnimationOptimizationSettings{Mode=AnimationOptimizationMode.HighQuality,BindBoundsDiagonal=1};
        var nearBind = Clip(Track("any identity", (6, new([new(0,3),new(4,3.00001f),new(8,3)]))));
        var nearResult = AnimationOptimization.Optimize(nearBind,["any identity"],quality with{BindPoses=settings.BindPoses});
        check(nearResult.Tracks.Length==1 && nearResult.RemovedBindTracks.Length==0 && nearResult.Tracks[0].Channels[6]!.Keys.Length==2,
            "A near-bind pulse reduced within HQ bounds retains its target track; only originally exact bind constants are removed");
        var baseline = Clip(Track("curve",(3,new([new(0,0),new(4,0)]))));
        var small = Track("curve",(3,new([new(0,0),new(4,.005f)])));
        var accepted = AnimationOptimization.CertifyCandidate(baseline,[small],quality);
        check(accepted.Accepted && accepted.MaximumValueBound is > .0049 and <= .005001 && accepted.Intervals > 0,
            "Decoded quantized coefficients receive an independent continuous Bernstein value and derivative bound");
        var large = Track("curve",(3,new([new(0,0),new(4,.02f)])));
        check(!AnimationOptimization.CertifyCandidate(baseline,[large],quality).Accepted,
            "A coefficient outside the unchanged Euler ceiling is rejected");
        var overshoot = Track("curve",(3,new([new(0,0,0,.1f),new(4,0,-.1f,0)])));
        check(!AnimationOptimization.CertifyCandidate(baseline,[overshoot],quality).Accepted,
            "Equal packed knot values cannot hide excessive fractional Hermite overshoot");
        var slowBound = quality with{EulerValueBoundDegrees=1,EulerSpeedBoundDegreesPerFrame=.001};
        check(!AnimationOptimization.CertifyCandidate(baseline,[small],slowBound).Accepted,
            "A small value change still fails when its continuous derivative exceeds the frozen speed bound");
        var cubic = new Curve(Enumerable.Range(0,5).Select(i=>new Key(i,i*i*i,3*i*i,3*i*i)).ToArray());
        var cubicClip = Clip(Track("curve",(3,cubic)));
        var endpoints = Track("curve",(3,new([cubic.Keys[0],cubic.Keys[^1]])));
        check(AnimationOptimization.CertifyCandidate(cubicClip,[endpoints],new()).Accepted,
            "The complete certificate compares previously reduced endpoints to every original polynomial interval");
        var loopBaseline = baseline with{Loop=true,Tracks=[Track("curve",(3,new([new(2,0),new(5,0)],LoopFrameLimit:9)))]};
        var loopOutput = Track("curve",(3,new([new(2,.005f),new(5,0)],LoopFrameLimit:9)));
        check(AnimationOptimization.CertifyCandidate(loopBaseline,[loopOutput],quality).Accepted,
            "Wrapped Hermite intervals before the first key and after the last key are partitioned and certified");
        var mandatory = quality with{MandatoryFrames=[4]};
        check(!AnimationOptimization.CertifyCandidate(baseline,[small],mandatory).Accepted,
            "A fitting endpoint requires exact decoded tuple bits even inside quality tolerances");
        var pair = new Curve([new(0,0),new(2,0),new(2,1),new(4,1)]);
        var alteredPair = new Curve([new(0,0),new(2,.001f),new(2,1),new(4,1)]);
        check(!AnimationOptimization.CertifyCandidate(Clip(Track("curve",(3,pair))),[Track("curve",(3,alteredPair))],quality).Accepted,
            "Incoming and outgoing paired-key sides remain exact under packed reduction");
        var exhausted = AnimationOptimization.CertifyCandidate(baseline,[small],quality with{MaximumWork=0});
        check(!exhausted.Accepted && exhausted.WorkLimitReached,
            "An exhausted proof budget rejects the candidate instead of accepting incomplete evidence");
        check(AnimationOptimization.CertifyCandidate(original,[],settings,targetBoneNames:["any identity"]).Accepted,
            "The full composed certificate independently proves an omitted native bind-constant track");
        check(!AnimationOptimization.CertifyCandidate(moving,[],settings,targetBoneNames:["any identity"]).Accepted,
            "The full composed certificate rejects omitted target motion with hidden tangent movement");
        check(!AnimationOptimization.CertifyCandidate(original,[],settings with{PreserveStructureBones=["any identity"]},targetBoneNames:[]).Accepted,
            "A protected orphan track cannot disappear through the absent-target certificate path");
        var tooMany = new Curve(Enumerable.Repeat(new Key(0,0),AnimationOptimization.MaximumInputKeys+1).ToArray());
        bool inputRejected=false, outputRejected=false;
        try{AnimationOptimization.CertifyCandidate(Clip(Track("curve",(3,tooMany))),[],quality);}catch(NotSupportedException){inputRejected=true;}
        try{AnimationOptimization.CertifyCandidate(baseline,[Track("curve",(3,tooMany))],quality);}catch(NotSupportedException){outputRejected=true;}
        check(inputRejected && outputRejected,"Each certificate input independently obeys the two-million-key limit even when its counterpart is small");
        using var cancelled = new CancellationTokenSource(); cancelled.Cancel(); bool stopped=false;
        try{AnimationOptimization.CertifyCandidate(baseline,[small],quality,cancelled.Token);}catch(OperationCanceledException){stopped=true;}
        check(stopped,"Cancellation aborts continuous candidate certification before publication");
        SparseProtections(check);
    }

    static void SparseProtections(Action<bool,string> check)
    {
        BoneTrack Track(Curve curve){var channels=new Curve?[9];channels[3]=curve;return new("sparse protected",channels);}
        AnimationClip Clip(Curve curve)=>new(Guid.NewGuid(),"Independent sparse protection",9,new(60,1),false,[Track(curve)]);
        var quality=new AnimationOptimizationSettings{Mode=AnimationOptimizationMode.HighQuality,BindBoundsDiagonal=1};
        var line=new Curve([new(0,1,.125f,.125f),new(8,2,.125f,.125f)]);
        const float shift=1f/1024;
        var shifted=line with{Keys=line.Keys.Select(k=>k with{Value=k.Value+shift}).ToArray()};
        var baseline=Clip(line);var candidate=Track(shifted);
        check(shifted.Sample(2.5)-line.Sample(2.5)==shift&&AnimationOptimization.CertifyCandidate(baseline,[candidate],quality).Accepted,
            "Independent sparse counterexample is a permitted 1/1024 Euler shift without authored protections");
        var protections=new[]{
            quality with{ProtectedRanges=[new(2,3,"Sparse protected interval")]},
            quality with{MandatoryFrames=[2.5]},
            quality with{ProtectedRanges=[new(2.5,2.5,"Sparse protected point")]},
            quality with{ChannelRules=[new("sparse protected",3){ProtectedRanges=[new(2,3,"Channel sparse interval")]}]},
            quality with{ChannelRules=[new("sparse protected",3){MandatoryFrames=[2.5]}]},
            quality with{ChannelRules=[new("sparse protected",3){ProtectedRanges=[new(2.5,2.5,"Channel sparse point")]}]}
        };
        for(int i=0;i<protections.Length;i++)
            check(!AnimationOptimization.CertifyCandidate(baseline,[candidate],protections[i]).Accepted,
                "Decoded coefficients cannot change a protected sparse interval or interior point without an original knot / "+i);
        foreach(var protection in protections)
            check(AnimationOptimization.CertifyCandidate(baseline,baseline.Tracks,protection).Accepted,
                "An identical sparse polynomial still certifies under interval and point protections");

        // q(t)=1+(t-2.5)/2048 agrees at the protected point but has a different
        // derivative. A point-only value check cannot certify the old segment.
        var flat=new Curve([new(0,1),new(8,1)]);const float slope=1f/2048;
        var crossing=new Curve([new(0,1-2.5f*slope,slope,slope),new(8,1+5.5f*slope,slope,slope)]);
        check(crossing.Sample(2.5)==1&&AnimationOptimization.CertifyCandidate(Clip(flat),[Track(crossing)],quality).Accepted,
            "Independent interior-point witness matches the exact value while changing its slope within ordinary HQ ceilings");
        check(!AnimationOptimization.CertifyCandidate(Clip(flat),[Track(crossing)],quality with{MandatoryFrames=[2.5]}).Accepted
            &&!AnimationOptimization.CertifyCandidate(Clip(flat),[Track(crossing)],quality with{ProtectedRanges=[new(2.5,2.5,"Preserve interior point segment")]}).Accepted,
            "Mandatory and zero-length protected interior points preserve the original polynomial and derivative, not just a coincident value");

        var joined=new Curve([new(0,1,.125f,.125f),new(4,1.5f,.125f,.125f),new(8,2,.125f,.125f)]);
        var changedMiddle=joined with{Keys=[joined.Keys[0],joined.Keys[1],joined.Keys[2] with{Value=2+shift}]};
        check(AnimationOptimization.CertifyCandidate(Clip(joined),[Track(changedMiddle)],quality with{MandatoryFrames=[4]}).Accepted
            &&AnimationOptimization.CertifyCandidate(Clip(joined),[Track(changedMiddle)],quality with{ProtectedRanges=[new(2,4,"Ending at authored join")]}).Accepted,
            "Exact authored join tuples and a touching range boundary still allow bounded approximation on the unprotected adjacent interval");

        var pulse=new Curve([new(0,1),new(4,1+shift),new(8,1)]);
        var reduced=AnimationOptimization.Optimize(Clip(pulse),["sparse protected"],quality);
        check(reduced.Tracks.Single().Channels[3]!.Keys.Length==2,
            "Independent small pulse is reducible under the frozen ordinary HQ bounds");
        foreach(bool channel in new[]{false,true})
        {
            var zeroRange=channel?quality with{ChannelRules=[new("sparse protected",3){ProtectedRanges=[new(2.5,2.5,"Sparse point")]}]}
                :quality with{ProtectedRanges=[new(2.5,2.5,"Sparse point")]};
            var held=AnimationOptimization.Optimize(Clip(pulse),["sparse protected"],zeroRange);
            check(ReferenceEquals(held.Tracks.Single().Channels[3],pulse),
                "A zero-length protected range strictly inside a proposed merge preserves the original surrounding pulse segment / channel="+channel);
        }
        var outside=AnimationOptimization.CertifyCandidate(baseline,[candidate],quality with{MandatoryFrames=[2.5],MaximumWork=0});
        check(!outside.Accepted&&outside.WorkLimitReached,"Sparse-protection certification retains its operation budget and fails closed when proof work is unavailable");
    }
}
