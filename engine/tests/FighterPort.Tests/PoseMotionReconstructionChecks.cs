using System.Numerics;
using FighterPort.Core;

// Public workflow checks use analytic Cartesian/curve expectations. They do
// not call BaselineMotionTransfer, the fitter or its private probe generator
// to produce their oracle, and contact fitting is explicitly disabled.
internal static class PoseMotionReconstructionChecks
{
    public static void Run(Action<bool,string> check)
    {
        foreach(var (sourceScale,targetScale) in new[]{(1f,2f),(2f,1f)})
        {
            Polynomial(sourceScale,targetScale,check);
            Held(sourceScale,targetScale,check);
            Turns(sourceScale,targetScale,0,check);
            Turns(sourceScale,targetScale,3,check);
            SphericalRefinement(sourceScale,targetScale,check);
        }
        ExactNativeCurves(check);
        AdaptiveDerivatives(check);
        MinimaxDerivatives(check);
        SufficientChannels(check);
        AuthoredSubframeWitnesses(check);
        TimingAndBounds(check);
        ResourceBudgets(check);
    }

    static void SufficientChannels(Action<bool,string> check)
    {
        // The zero-endpoint cubic 0.004*t*(1-t) has maximum .001.
        // Its tangent error is deliberately correlated with another joint's
        // motion; an acceptable scalar must not acquire different coefficients
        // merely because another channel requires repair.
        var initial=new Curve([new(0,0,.004f,.004f),new(1,0,-.004f,-.004f)]);
        double[] times=[.125,.375,.625,.875];float[] values=[0,0,0,0];
        long budget=SharedSlopeMinimax.MaximumWork;
        var fit=PoseMotionReconstruction.RefineCurve(initial,times,values,.001f,ref budget);
        check(ReferenceEquals(fit.Curve,initial)&&!fit.Improved&&fit.Passes==0&&fit.MinimaxCalls==0&&budget==SharedSlopeMinimax.MaximumWork,
            "A sufficient scalar channel retains its exact native derivatives without consuming refinement work; every channel remains independently validated");
        var smaller=PoseMotionReconstruction.RefineCurve(initial,times,values,.0001f);
        check(smaller.Improved&&Enumerable.Range(0,257).Max(i=>Math.Abs(smaller.Curve.Sample(i/256d)))<1e-7&&
            smaller.Curve.Keys.Select(k=>k.Value).SequenceEqual(initial.Keys.Select(k=>k.Value)),
            "The same independent cubic is repaired when it violates the actual bound, without altering its fixed native values");
    }

    static void MinimaxDerivatives(Action<bool,string> check)
    {
        // Independent equioscillation oracle: for endpoints zero and targets
        // (1,0,1) at quarters, m*t*(1-t) has optimum m=16/7 and error4/7.
        SharedSlopeMinimax.Equation[] rows=[new(0,.140625,-.046875,1),new(0,.125,-.125,0),new(0,.046875,-.140625,1),
            // At nextafter(0), the second basis term underflows to signed0;
            // it must remain a valid unilateral strip, not reject the clip.
            new(0,double.Epsilon,0,0)];
        long remaining=SharedSlopeMinimax.MaximumWork;
        var fit=SharedSlopeMinimax.Fit(2,rows,[0,0],ref remaining,default);
        check(Math.Abs(fit.Slopes[0]-16d/7)<1e-6&&Math.Abs(fit.Slopes[1]+16d/7)<1e-6&&Math.Abs(fit.FittingError-4d/7)<1e-8,
            "Integer shared-slope minimax matches an independently derived equioscillation optimum");
        check(fit.Calls==32&&fit.Work>0&&remaining==SharedSlopeMinimax.MaximumWork-fit.Work,
            "Minimax receipt counts bounded feasibility calls and debits the aggregate work budget");
        // Directly expanded two cubics: f0=5t^2-5t^3+2t,
        // f1=2-3u-7u^2+7u^3; derivative at shared knot is -3.
        double[] times=[.125,.375,.625,.875,1.125,1.375,1.625,1.875];
        double Expected(double t)=>t<1?5*t*t-5*t*t*t+2*t:2-3*(t-1)-7*(t-1)*(t-1)+7*(t-1)*(t-1)*(t-1);
        var values=times.Select(t=>(float)Expected(t)).ToArray();
        var initial=new Curve([new(0,0),new(1,2),new(2,-1)]);
        var refined=PoseMotionReconstruction.RefineCurve(initial,times,values,.00001f);
        double maximum=Enumerable.Range(0,257).Max(i=>Math.Abs(refined.Curve.Sample(i/128d)-Expected(i/128d)));
        check(maximum<1e-6&&refined.Curve.Keys.Select(k=>k.Value).SequenceEqual(new float[]{0,2,-1})&&
            refined.Curve.Keys.All(k=>k.InTangent==k.OutTangent),
            "Coupled integer native derivatives reproduce two independent expanded cubics at withheld times without changing values or splitting tangents");
        var repeated=SharedSlopeMinimax.Fit(2,rows,[0,0],ref remaining,default);
        check(fit.Slopes.SequenceEqual(repeated.Slopes),"Integer minimax fitting is deterministic across repeated solves");
        var coupled=SharedSlopeMinimax.Fit(3,[new(0,.125,-.125,-.125),new(0,.140625,-.046875,.046875),
            new(1,.125,-.125,-.125),new(1,.046875,-.140625,-.328125)],[0,0,0],ref remaining,default);
        check(coupled.FittingError<1e-8&&coupled.Slopes.Zip(new double[]{1,2,3}).All(p=>Math.Abs(p.First-p.Second)<1e-6),
            "Affine-envelope projection recovers independently supplied coupled exact derivatives across a shared native knot");
        bool budget=false,cancel=false;
        long zero=0;var bounded=SharedSlopeMinimax.Fit(2,rows,[0,0],ref zero,default);
        budget=bounded.WorkLimitReached&&bounded.Calls==0&&bounded.Work==0&&bounded.Slopes.SequenceEqual(new double[]{0,0})&&bounded.FittingError==1;
        using var cancellation=new CancellationTokenSource();cancellation.Cancel();
        try{SharedSlopeMinimax.Fit(2,rows,[0,0],ref remaining,cancellation.Token);}catch(OperationCanceledException){cancel=true;}
        check(budget&&cancel,"Integer minimax reports an unfinished zero-budget candidate and honors cancellation without altering keys or bounds");
        var longRows=Enumerable.Range(0,199).SelectMany(s=>new[]{new SharedSlopeMinimax.Equation(s,.125,-.125,1),new SharedSlopeMinimax.Equation(s,.125,-.125,-1)}).ToArray();
        long limited=50000;var early=SharedSlopeMinimax.Fit(200,longRows,new double[200],ref limited,default);
        check(early.Calls==32&&early.Work<50000&&early.FittingError==1&&limited==50000-early.Work,
            "A long contradictory strip path charges only executed arrays/projections/pairs, not32 complete traversals it never executes, under the same finite aggregate ceiling");
    }

    static void Polynomial(float sourceScale,float targetScale,Action<bool,string> check)
    {
        var source=Fixture("polynomial-original",sourceScale);var target=Fixture("polynomial-destination",targetScale);
        var root=Channels();root[6]=PolynomialCurve(t=>sourceScale*Route(t),t=>sourceScale*RouteDerivative(t));
        var body=Channels();body[7]=PolynomialCurve(t=>sourceScale*(2+Lower(t)),t=>sourceScale*LowerDerivative(t));
        var tip=Channels();tip[0]=PolynomialCurve(Stretch,StretchDerivative);tip[6]=PolynomialCurve(t=>sourceScale*(1+Reach(t)),t=>sourceScale*ReachDerivative(t));
        var clip=new AnimationClip(Guid.NewGuid(),"Independent cubic motion",5,new(60),false,
            [new(source.Scene.Bones[0].Name,root),new(source.Scene.Bones[1].Name,body),new(source.Scene.Bones[3].Name,tip)]);
        var bake=Bake(source,target,clip);var reconstruction=bake.Clip.Retarget?.Pipeline?.Reconstruction;
        check(reconstruction is {Passed:true,ExactNativeCurves:false,KeyValueUpdates:0}&&reconstruction.ReferenceSamples==81&&bake.Clip.LastKeyFrame==5,
            $"Public reconstruction preserves fixed integer values and the distinct closing key for independently authored cubic motion ({sourceScale}→{targetScale})");
        float integerError=0,channelError=0,skinError=0;
        var times=new[]{0,.017,.137,.419,.773,1,1.113,1.509,1.941,2,2.271,2.693,3,3.013,3.607,3.991,4,4.049,4.567,4.987,5};
        foreach(double frame in times)
        {
            var pose=AnimationService.Evaluate(target.Scene,bake.Clip,frame);
            var expected=target.Scene.Bones.Select(b=>b.Rest).ToArray();
            expected[0]=expected[0] with{Translation=new(targetScale*(float)Route(frame),0,0)};
            expected[1]=expected[1] with{Translation=new(0,targetScale*(2+(float)Lower(frame)),0)};
            expected[3]=expected[3] with{Translation=new(targetScale*(1+(float)Reach(frame)),0,0),Scale=new((float)Stretch(frame),1,1)};
            for(int bone=0;bone<expected.Length;bone++)for(int channel=0;channel<9;channel++)
            {
                float error=Math.Abs(pose.Local[bone].Channels()[channel]-expected[bone].Channels()[channel]);
                channelError=Math.Max(channelError,error);if(frame==Math.Truncate(frame))integerError=Math.Max(integerError,error);
            }
            foreach(var vertex in target.Scene.Meshes[0].Vertices)
            {
                // The triangle has25%body/75%tip skin. Body contributes root
                // travel/lowering; tip additionally contributes local reach
                // and scale about its unchanged native rest pivot(1s,2s,0).
                double dx=vertex.Position.X-targetScale,weight=.75;
                var expectedSkin=new Vector3((float)(targetScale*Route(frame)+vertex.Position.X+weight*(targetScale*Reach(frame)+(Stretch(frame)-1)*dx)),
                    (float)(vertex.Position.Y+targetScale*Lower(frame)),vertex.Position.Z);
                skinError=Math.Max(skinError,Vector3.Distance(AnimationService.Skin(vertex,pose),expectedSkin));
            }
        }
        check(integerError<.00001f&&bake.Clip.Tracks.SelectMany(t=>t.Channels.OfType<Curve>()).All(c=>c.Keys.Select(k=>k.Frame).SequenceEqual(new double[]{0,1,2,3,4,5})),
            $"Every integer pose channel equals the independent polynomial oracle and key times remain fixed ({sourceScale}→{targetScale}, max{integerError:G9})");
        check(channelError<.00002f&&skinError<.00003f,
            $"Withheld fractional native channels and blended skin match independent cubic and Cartesian expectations ({sourceScale}→{targetScale}, channel{channelError:G9}, skin{skinError:G9})");
        check(bake.Contacts.Length==0&&bake.Clip.Retarget is {IsDraft:true}&&reconstruction!.WithheldSamples>reconstruction.ReferenceSamples/4,
            "Representation fidelity does not invent contacts or turn an unvalidated rewrite draft into an Apply-ready animation");
    }

    static void Held(float sourceScale,float targetScale,Action<bool,string> check)
    {
        var source=Fixture("held-original",sourceScale);var target=Fixture("held-destination",targetScale);
        var channels=Channels();channels[6]=new([new(0,sourceScale),new(1,1.4f*sourceScale),new(3,.8f*sourceScale)],Interpolation.Step);
        var clip=new AnimationClip(Guid.NewGuid(),"Independent held motion",4,new(60),false,[new(source.Scene.Bones[3].Name,channels)]);
        var bake=Bake(source,target,clip);float error=0;
        foreach(double frame in new[]{0,.09,.999999,1,1.000001,1.527,2,2.999999,3,3.9999})
        {
            double held=frame<1?1:frame<3?1.4:.8;
            var pose=AnimationService.Evaluate(target.Scene,bake.Clip,frame);
            error=Math.Max(error,Math.Abs(pose.Local[3].Translation.X-(float)(held*targetScale)));
            foreach(var vertex in target.Scene.Meshes[0].Vertices)
            {
                var expected=vertex.Position+new Vector3((float)(.75*targetScale*(held-1)),0,0);
                error=Math.Max(error,Vector3.Distance(AnimationService.Skin(vertex,pose),expected));
            }
        }
        check(error<.000003f&&bake.Clip.Tracks.SelectMany(t=>t.Channels.OfType<Curve>()).All(c=>c.Interpolation==Interpolation.Step)&&
              bake.Clip.Retarget?.Pipeline?.Reconstruction is {Passed:true,KeyValueUpdates:0},
            $"Cross-rig held channels retain independent before/at/after boundary poses and skin without interpolation ({sourceScale}→{targetScale}, max{error:G9})");
    }

    static void Turns(float sourceScale,float targetScale,int animatedBone,Action<bool,string> check)
    {
        var source=Fixture("turn-original",sourceScale);var target=Fixture("turn-destination",targetScale);
        var channels=Channels();channels[5]=new([new(0,0),new(4,720)],Interpolation.Linear);
        var clip=new AnimationClip(Guid.NewGuid(),"Independent complete turns",5,new(60),false,[new(source.Scene.Bones[animatedBone].Name,channels)]);
        var bake=Bake(source,target,clip);var output=bake.Clip.Tracks.Single(t=>t.Bone==target.Scene.Bones[animatedBone].Name).Channels[5]!;
        float channelError=0,skinError=0;
        foreach(double frame in new[]{0,.031,.173,.531,.997,1,1.417,1.991,2,2.083,2.713,3,3.209,3.991,4})
        {
            double degrees=180*frame,angle=Math.PI*degrees/180,cos=Math.Cos(angle),sin=Math.Sin(angle);
            var pose=AnimationService.Evaluate(target.Scene,bake.Clip,frame);
            channelError=Math.Max(channelError,Math.Abs(output.Sample(frame)-(float)degrees));
            foreach(var vertex in target.Scene.Meshes[0].Vertices)
            {
                Vector3 expected;
                if(animatedBone==0)expected=new((float)(cos*vertex.Position.X-sin*vertex.Position.Y),(float)(sin*vertex.Position.X+cos*vertex.Position.Y),vertex.Position.Z);
                else
                {
                    double dx=vertex.Position.X-targetScale,dy=vertex.Position.Y-2*targetScale;
                    expected=new((float)(vertex.Position.X+.75*((cos-1)*dx-sin*dy)),(float)(vertex.Position.Y+.75*(sin*dx+(cos-1)*dy)),vertex.Position.Z);
                }
                skinError=Math.Max(skinError,Vector3.Distance(AnimationService.Skin(vertex,pose),expected));
            }
        }
        check(Math.Abs(output.Keys[^1].Value-720)<.0001f&&channelError<.0003f&&skinError<.00003f&&
              bake.Clip.Retarget?.Pipeline?.Reconstruction is {Passed:true,KeyValueUpdates:0},
            $"Two intentional complete turns survive independently checked {(animatedBone==0?"actor":"detailed native bone")} channels and skinned geometry ({sourceScale}→{targetScale}, channel{channelError:G9}, skin{skinError:G9})");
    }

    static void ExactNativeCurves(Action<bool,string> check)
    {
        var source=Fixture("authentic",1);var spin=Channels();spin[5]=new([new(0,0,180,180),new(4,720,180,180)],Interpolation.Hermite,6);
        var detail=Channels();detail[0]=new([new(0,1),new(4,1.2f)],Interpolation.Linear);detail[6]=new([new(0,1),new(2.5,1.3f)],Interpolation.Step);
        var clip=new AnimationClip(Guid.NewGuid(),"Exact mixed native channels",5,new(60000,1001),true,[new(source.Scene.Bones[0].Name,spin),new(source.Scene.Bones[3].Name,detail)]);
        var bake=Bake(source,source,clip);var stored=bake.Clip.Tracks.Single(t=>t.Bone==source.Scene.Bones[0].Name).Channels[5]!;
        check(stored.Keys.SequenceEqual(spin[5]!.Keys)&&!ReferenceEquals(stored.Keys,spin[5]!.Keys)&&stored.LoopFrameLimit==6&&stored.Interpolation==Interpolation.Hermite&&
              bake.Clip.Tracks.Single(t=>t.Bone==source.Scene.Bones[3].Name).Channels[6]!.Interpolation==Interpolation.Step&&bake.Clip.Rate==new RationalRate(60000,1001)&&bake.Clip.Loop&&
              bake.Clip.Retarget?.Pipeline?.Reconstruction is {Passed:true,ExactNativeCurves:true,KeyValueUpdates:0},
            "Same-rig exact reconstruction deep-copies winding/tangents/native closure/held fractional keys and rational timing instead of fitting them");
        float skinError=0;
        foreach(double frame in new[]{0,.173,1.417,2.499999,2.5,2.500001,3.713,4,4.713,5})
        {
            var original=AnimationService.Evaluate(source.Scene,clip,frame);var output=AnimationService.Evaluate(source.Scene,bake.Clip,frame);
            foreach(var vertex in source.Scene.Meshes[0].Vertices)skinError=Math.Max(skinError,Vector3.Distance(AnimationService.Skin(vertex,original),AnimationService.Skin(vertex,output)));
        }
        check(skinError==0&&spin[5]!.Keys[^1].Value==720&&bake.Clip.Id!=clip.Id&&bake.Clip.Revision!=clip.Revision,
            "Exact mixed-channel reuse retains authentic skin at independent fractional/held/closing samples and creates independent target motion identity");
    }

    static void SphericalRefinement(float sourceScale,float targetScale,Action<bool,string> check)
    {
        var source=Fixture("spherical-original",sourceScale);var target=Fixture("spherical-destination",targetScale);
        var axis=Vector3.Normalize(new Vector3(1,2,3));var originalKeys=new QuaternionKey[]{new(0,Quaternion.Identity),new(1,Quaternion.CreateFromAxisAngle(axis,2*MathF.PI/3))};
        var clip=new AnimationClip(Guid.NewGuid(),"Independent oblique rotation",2,new(60),false,
            [new(source.Scene.Bones[3].Name,Channels()){Rotation=new(originalKeys.ToArray())}]);
        string deform=AnimationControls.DeformChecksum(target.Scene);var bake=Bake(source,target,clip);var receipt=bake.Clip.Retarget!.Pipeline!.Reconstruction!;
        check(receipt is {RefinedOutputSelected:true,RefinedChannels:>0,KeyValueUpdates:0,Passed:true,AddedFractionalKeys:>0}&&receipt.MaximumSkinError<receipt.InitialMaximumSkinError&&
              receipt.MaximumRotationRadians<receipt.InitialMaximumRotationRadians&&receipt.MaximumEulerChannelDegrees<receipt.InitialMaximumEulerChannelDegrees&&
              receipt.ReservedWithheldSamples>=6&&receipt.AdaptiveFittingSamples+receipt.ReservedWithheldSamples==receipt.WithheldSamples,
            $"The public workflow represents oblique native motion with actual fractional knots under unchanged full-skin/channel bounds ({sourceScale}→{targetScale})");
        // Rodrigues' Cartesian construction is independent of the quaternion/
        // Euler decoder, derivative fitter and its probe list. Source and
        // destination share a reviewed axis frame, not an intermediate rig.
        float maximum=0;var pivot=new Vector3(targetScale,2*targetScale,0);
        foreach(double frame in Enumerable.Range(0,257).Select(i=>(i+.3141592653589793)/257).Concat(new double[]{0,1}))
        {
            var pose=AnimationService.Evaluate(target.Scene,bake.Clip,frame);double angle=2*Math.PI*frame/3;
            foreach(var vertex in target.Scene.Meshes[0].Vertices)
            {
                var v=vertex.Position-pivot;
                var moved=v*(float)Math.Cos(angle)+Vector3.Cross(axis,v)*(float)Math.Sin(angle)+axis*(Vector3.Dot(axis,v)*(float)(1-Math.Cos(angle)));
                var expected=vertex.Position*.25f+(pivot+moved)*.75f;
                maximum=Math.Max(maximum,Vector3.Distance(AnimationService.Skin(vertex,pose),expected));
            }
        }
        check(maximum<targetScale*.0001f&&AnimationControls.DeformChecksum(target.Scene)==deform&&clip.Tracks[0].Rotation!.Keys.SequenceEqual(originalKeys)&&
              bake.Clip.Tracks.SelectMany(t=>t.Channels.OfType<Curve>()).All(c=>c.Keys.Count(k=>k.Frame==Math.Truncate(k.Frame))==2&&c.Keys.All(k=>k.InTangent==k.OutTangent)),
            $"Independent withheld Cartesian skin confirms the selected refinement while native integers, source quaternion data and target rig stay intact ({sourceScale}→{targetScale}, error{maximum:G9})");
    }

    static void AuthoredSubframeWitnesses(Action<bool,string> check)
    {
        var source=Fixture("native-knot-original",1);var target=Fixture("native-knot-destination",2);
        AnimationClip Pulse(int bone,int channel,float rest,float peak)
        {
            var channels=Channels();channels[channel]=new([new(0,rest),new(.03125,peak),new(.0625,rest),new(1,rest)]);
            return new(Guid.NewGuid(),"Independent narrow native interval",2,new(60),false,[new(source.Scene.Bones[bone].Name,channels)]);
        }
        var rotation=Pulse(3,5,0,90);var rotated=Bake(source,target,rotation);var rotational=rotated.Clip.Retarget!.Pipeline!.Reconstruction!;
        check(!rotational.Passed&&rotational.MaximumRotationRadians>rotational.RotationBound&&rotational.MaximumEulerChannelDegrees>rotational.EulerChannelBoundDegrees&&
              rotated.Clip.Tracks.Single(t=>t.Bone==target.Scene.Bones[3].Name).Channels[5]!.Keys.All(k=>k.Value==0)&&rotational.KeyValueUpdates==0,
            "A90-degree authored subframe pulse is measured at its actual native knot and remains an unrepresentable native draft without moving integer keys");
        // Bone2 has no weights or descendants with weights: geometric output
        // alone cannot rate preservation of its meaningful native channels.
        var translation=Bake(source,target,Pulse(2,6,0,1));var translated=translation.Clip.Retarget!.Pipeline!.Reconstruction!;
        check(!translated.Passed&&translated.MaximumSkinError==0&&translated.MaximumRotationRadians==0&&translated.MaximumTranslationError>translated.TranslationBound,
            "An unweighted native helper translation pulse fails its separate channel bound despite exact zero skin/angular error");
        var scale=Bake(source,target,Pulse(2,0,1,1.5f));var scaled=scale.Clip.Retarget!.Pipeline!.Reconstruction!;
        check(!scaled.Passed&&scaled.MaximumSkinError==0&&scaled.MaximumRotationRadians==0&&scaled.MaximumScaleError>scaled.ScaleBound,
            "An unweighted native helper scale pulse fails its dimensionless channel bound despite exact zero skin/angular error");
        var turns=Bake(source,target,Pulse(3,5,0,720));var winding=turns.Clip.Retarget!.Pipeline!.Reconstruction!;
        check(!winding.Passed&&winding.MaximumEulerChannelDegrees>winding.EulerChannelBoundDegrees&&turns.Clip.Tracks.SelectMany(t=>t.Channels.OfType<Curve>()).All(c=>c.Keys.Count(k=>k.Frame==Math.Truncate(k.Frame))==2),
            "Chronological native-knot/extrema refinement exposes a narrow720-degree turn instead of deriving winding from an aliased fit lattice");
        var preserved=Bake(source,source,rotation);var exact=preserved.Clip.Retarget!.Pipeline!.Reconstruction!;
        check(exact is {Passed:true,ExactNativeCurves:true,MaximumSkinError:0,MaximumTranslationError:0,MaximumScaleError:0,MaximumEulerChannelDegrees:0}&&
              preserved.Clip.Tracks.Single(t=>t.Bone==source.Scene.Bones[3].Name).Channels[5]!.Keys.Single(k=>k.Frame==.03125).Value==90,
            "Exact same-rig reuse keeps the original narrow native knot and still passes independent channel/geometry representation checks");
        check(winding.WithheldSamples<=PoseMotionReconstruction.MaximumProbes&&winding.WithheldSamples*target.Scene.Bones.Length<=PoseMotionReconstruction.MaximumBonePoses&&
              winding.ProbeCoverage.Contains("native keys",StringComparison.Ordinal),
            "Native-knot and winding evidence reports finite bounded probe coverage separately from continuous-time correctness");
    }

    static void AdaptiveDerivatives(Action<bool,string> check)
    {
        // For f(t)=t^4 on[0,1], the continuous-L2 endpoint-constrained
        // cubic has independently integrated slopes3/14 and53/14. Its
        // Chebyshev error is not minimal. No production fitter supplies this
        // reference, and withheld times never enter the collocation set.
        var initial=new Curve([new(0,0,3f/14,3f/14),new(1,1,53f/14,53f/14)]);
        var before=initial.Keys.ToArray();var times=Enumerable.Range(0,65).Select(i=>i/64d).ToArray();
        var values=times.Select(t=>(float)(t*t*t*t)).ToArray();
        var result=PoseMotionReconstruction.RefineCurve(initial,times,values,.001f);
        double initialError=0,error=0;
        foreach(double t in Enumerable.Range(0,2000).Select(i=>(i+.3141592653589793)/2000))
        {
            double expected=t*t*t*t;initialError=Math.Max(initialError,Math.Abs(initial.Sample(t)-expected));error=Math.Max(error,Math.Abs(result.Curve.Sample(t)-expected));
        }
        check(result.Improved&&result.Passes is >0 and <=PoseMotionReconstruction.MaximumRefinementPasses&&error<initialError*.99,
            $"Adaptive native shared derivatives reduce independent withheld quartic maximum error rather than optimizing only lattice mean error ({initialError:G9}→{error:G9})");
        check(initial.Keys.SequenceEqual(before)&&result.Curve.Keys.Select(k=>(k.Frame,k.Value)).SequenceEqual(before.Select(k=>(k.Frame,k.Value)))&&result.Curve.Keys.All(k=>k.InTangent==k.OutTangent),
            "Adaptive reconstruction leaves original integer key values/times intact and writes exactly one shared native tangent per key");
        var repeated=PoseMotionReconstruction.RefineCurve(initial,times,values,.001f);
        check(result.Curve.Keys.SequenceEqual(repeated.Curve.Keys)&&result.Error==repeated.Error,
            "Adaptive derivative fitting is deterministic and does not depend on playback or a previous solve");
        var scaled=PoseMotionReconstruction.RefineCurve(initial with{Keys=initial.Keys.Select(k=>k with{Value=k.Value*1000,InTangent=k.InTangent*1000,OutTangent=k.OutTangent*1000}).ToArray()},times,values.Select(v=>v*1000).ToArray(),1);
        check(scaled.Improved&&Enumerable.Range(0,101).All(i=>Math.Abs(scaled.Curve.Sample(i/100d)/1000-result.Curve.Sample(i/100d))<.000002),
            "Adaptive translation derivatives commute with an independently applied file-unit scale");
        var cubicInitial=new Curve([new(0,0),new(1,1)]);var cubic=PoseMotionReconstruction.RefineCurve(cubicInitial,times,times.Select(t=>(float)(t*t*t)).ToArray(),.0001f);
        check(Enumerable.Range(0,101).All(i=>Math.Abs(cubic.Curve.Sample((i+.271828)/102)-Math.Pow((i+.271828)/102,3))<.000001)&&Math.Abs(cubic.Curve.Keys[0].OutTangent)<.00001&&Math.Abs(cubic.Curve.Keys[1].InTangent-3)<.00001,
            "Adaptive derivatives recover an independently authored representable cubic at withheld times with fixed native endpoints");
        var jump=PoseMotionReconstruction.RefineCurve(new([new(0,0),new(1,1)]),times,times.Select(t=>t<.47?0f:1f).ToArray(),.0001f);
        check(jump.Error>.25&&jump.Curve.Keys[0].Value==0&&jump.Curve.Keys[1].Value==1,
            "A discontinuous reference is not manufactured into an accurate continuous native curve by derivative refinement");
        bool cancelled=false,nonfinite=false,unordered=false,budget=false;
        try{PoseMotionReconstruction.RefineCurve(initial,times,values,.001f,new CancellationToken(true));}catch(OperationCanceledException){cancelled=true;}
        try{PoseMotionReconstruction.RefineCurve(initial,[0,double.NaN,1],[0,0,1],.001f);}catch(ArgumentException){nonfinite=true;}
        try{PoseMotionReconstruction.RefineCurve(initial,[0,.7,.2,1],[0,0,0,1],.001f);}catch(ArgumentException){unordered=true;}
        try{PoseMotionReconstruction.RefineCurve(initial,new double[PoseMotionReconstruction.MaximumProbes+1],new float[PoseMotionReconstruction.MaximumProbes+1],.001f);}catch(NotSupportedException){budget=true;}
        check(cancelled&&nonfinite&&unordered&&budget,"Adaptive native fitting cancels before work and rejects nonfinite, unordered or oversized collocation inputs");
    }

    static void TimingAndBounds(Action<bool,string> check)
    {
        var source=Fixture("timed-original",1);var target=Fixture("timed-destination",2);
        var channels=Channels();channels[6]=new([new(0,0),new(8,2.4f)],Interpolation.Linear);
        var clip=new AnimationClip(Guid.NewGuid(),"Independently timed route",8,new(60),true,[new(source.Scene.Bones[0].Name,channels)]);
        source=source with{Clips=[clip]};
        OperationResult<RetargetBake> Prepare(RetargetTiming time,RationalRate? rate=null,AnimationClip? input=null,CancellationToken cancellation=default)
        {
            var selected=input??clip;
            return RetargetService.Bake(source with{Clips=[selected]},selected,source.ControlRigs[0],target,target.ControlRigs[0],useContacts:false,
                fitting:new(){AutomaticContacts=false},timing:time,targetRate:rate,cancellation:cancellation);
        }
        var timing=new RetargetTiming{TrimStartSeconds=2d/60,TrimEndSeconds=6d/60,ExportSpeed=2};
        var sped=Prepare(timing).Value??throw new InvalidOperationException("Exact trim/speed draft failed.");
        float error=0;
        foreach(double frame in new[]{0d,.125,.375,.9,1,1.875,1.9999,2})
        {
            // Target units are twice source units. The source route is0.3
            // source units/frame; trim+2x speed maps f to2+2f exactly.
            float expected=(float)(.6*(2+2*frame));
            error=Math.Max(error,Math.Abs(AnimationService.Evaluate(target.Scene,sped.Clip,frame).World[0].Translation.X-expected));
        }
        check(sped.Clip.SampleCount==2&&sped.Clip.Rate==new RationalRate(60)&&!sped.Clip.Loop&&sped.Clip.LastKeyFrame==2&&error<.00001f,
            "Trim and export speed jointly preserve independent displayed route poses, end-exclusive sample count and a distinct native closing key");
        check(Math.Abs(sped.Clip.Retarget!.SourceSecondsAt(.375/60)-(2+2*.375)/60)<1e-12&&sped.Source.Clip.SampleCount==8&&sped.Source.Clip.Rate==new RationalRate(60)&&sped.Source.Clip.Loop,
            "Derived timing maps comparison back to authentic source seconds without rewriting source timing or loop metadata");
        var slowerRate=Prepare(timing with{ExportSpeed=1},new(30)).Value??throw new InvalidOperationException("Exact rational-rate conversion failed.");
        check(slowerRate.Clip.SampleCount==2&&slowerRate.Clip.Rate==new RationalRate(30)&&Math.Abs(AnimationService.Evaluate(target.Scene,slowerRate.Clip,1).World[0].Translation.X-2.4f)<.00001f,
            "Independent destination rate changes frame coordinates through seconds rather than copying source frame numbers");
        var document=new EditorDocument(RetargetService.Apply(target,sped));var prior=document.Project;
        var before=RetargetDependencies.Hash(document.Clip);document.Clock.Speed=2.5;document.Clock.Play(document.Clip);document.Clock.Advance(.005,document.Clip);document.Seek(.375);
        check(ReferenceEquals(document.Project,prior)&&!document.Dirty&&!document.CanUndo&&RetargetDependencies.Hash(document.Clip)==before,
            "Preview speed, playback and fractional scrub leave export keys, recipe, duration, source timing and history untouched");
        var fractional=Prepare(timing with{ExportSpeed=1.3});
        check(fractional.Value is null&&fractional.Diagnostics.Any(d=>d.Code=="RETARGET_UNSUPPORTED"&&d.Message.Contains("nonintegral",StringComparison.Ordinal)),
            "Nonintegral destination sample counts fail explicitly instead of rounding native timing");
        var oversized=Prepare(new(),input:clip with{SampleCount=200000});
        bool cancelled=false;try{Prepare(new(),cancellation:new CancellationToken(true));}catch(OperationCanceledException){cancelled=true;}
        check(oversized.Value is null&&oversized.Diagnostics.Any(d=>d.Code=="RETARGET_UNSUPPORTED"&&d.Message.Contains("before sampling",StringComparison.Ordinal))&&cancelled,
            "Oversized clips reject before winding traversal, and cancellation is observed without issuing a partial result");
    }

    static RetargetBake Bake(FighterProject source,FighterProject target,AnimationClip clip)
    {
        var result=RetargetService.Bake(source with{Clips=[clip]},clip,source.ControlRigs[0],target,target.ControlRigs[0],useContacts:false,
            fitting:new(){AutomaticContacts=false});
        if(result.Value is null)throw new InvalidOperationException("Public fixed-pose reconstruction failed: "+string.Join("; ",result.Diagnostics.Select(d=>d.Code+": "+d.Message)));
        return result.Value;
    }
    static void ResourceBudgets(Action<bool,string> check)
    {
        var source=Fixture("resource-original",1);var target=Fixture("resource-destination",2);
        // These invalid sampled scales are deliberate sentinels. A missing
        // preflight guard fails immediately at frame0 rather than running an
        // expensive regression test or obtaining its expected value from the
        // production cost estimator.
        AnimationClip Sentinel(int samples,Key[]? nativeKeys=null)
        {
            var channels=Channels();channels[0]=new(nativeKeys??[new(0,0)]);
            return new(Guid.NewGuid(),"Unsampled resource sentinel",samples,new(60),false,[new(source.Scene.Bones[0].Name,channels)]);
        }
        OperationResult<RetargetBake> Prepare(FighterProject input,FighterProject destination,AnimationClip clip,CancellationToken cancellation=default)
            =>RetargetService.Bake(input with{Clips=[clip]},clip,input.ControlRigs[0],destination,destination.ControlRigs[0],useContacts:false,
                fitting:new(){AutomaticContacts=false},cancellation:cancellation);
        var wideBones=source.Scene.Bones.Concat(Enumerable.Range(4,124).Select(i=>new Bone("Unweighted native branch"+i,0,Transform.Identity with{Translation=new(0,0,i*.01f)}))).ToArray();
        var wideScene=source.Scene with{Bones=wideBones,SkeletonRevision=Guid.NewGuid()};
        var wide=source with{Scene=wideScene,ControlRigs=[source.ControlRigs[0] with{SkeletonFingerprint=AnimationControls.Fingerprint(wideScene)}]};
        var combined=Prepare(wide,target,Sentinel(18000));
        //287985 mandatory probes×4 target bones is below2m, while the
        //128-source+twice4-target cost exceeds39m. Source work must count.
        check(combined.Value is null&&combined.Diagnostics.Any(d=>d.Code=="RETARGET_UNSUPPORTED"&&d.Message.Contains("combined source/target",StringComparison.Ordinal)&&d.Message.Contains("before sampling",StringComparison.Ordinal)),
            "A large source graph cannot hide behind a small target palette; combined source/twice-target work rejects before the invalid pose sentinel is sampled");
        FighterProject Dense(int vertices)
        {
            var original=target.Scene.Meshes[0];var dense=original with{Vertices=Enumerable.Repeat(original.Vertices[0],vertices).ToArray()};
            return target with{Scene=target.Scene with{Meshes=[dense]}};
        }
        var denseTarget=Dense(10000);var skin=Prepare(source,denseTarget,Sentinel(4000));
        //63985 mandatory probes×10000 vertices×reference/output is1.2797b
        //skin calls; bone work is small. No actual skin sweep is necessary.
        check(skin.Value is null&&skin.Diagnostics.Any(d=>d.Code=="RETARGET_UNSUPPORTED"&&d.Message.Contains("full-skin validation resource budget before sampling",StringComparison.Ordinal)),
            "Predictable full-skin work counts reference and output and rejects a dense mesh before fitting or sampling, independently of bone limits");
        var nativeKeys=Enumerable.Range(0,4300).Select(i=>new Key(i/4299d,0)).ToArray();
        var native=Prepare(source,Dense(20000),Sentinel(2,nativeKeys));
        check(native.Value is null&&native.Diagnostics.Any(d=>d.Code=="RETARGET_UNSUPPORTED"&&d.Message.Contains("probe validation budget",StringComparison.Ordinal)),
            "Native-knot and adjacent probes enforce the full-skin budget before fitting even when the short fit lattice alone would fit");
        var targetBones=target.Scene.Bones.Concat(Enumerable.Range(4,124).Select(i=>new Bone("Extra destination branch"+i,0,Transform.Identity with{Translation=new(0,0,i*.01f)}))).ToArray();
        var curveScene=target.Scene with{Bones=targetBones,SkeletonRevision=Guid.NewGuid()};var curveTarget=target with{Scene=curveScene,ControlRigs=[target.ControlRigs[0] with{SkeletonFingerprint=AnimationControls.Fingerprint(curveScene)}]};
        // Dense fractional interval coverage now reaches the per-target probe
        // limit sooner. Use independently wide SOURCE and target graphs so
        // this case still isolates aggregate work below the per-target limit.
        var aggregate=Prepare(wide,curveTarget,Sentinel(220));
        check(aggregate.Value is null&&aggregate.Diagnostics.Any(d=>d.Code=="RETARGET_UNSUPPORTED"&&d.Message.Contains("aggregate prepared/reference/candidate bone-work resource budget before sampling",StringComparison.Ordinal)),
            "Prepared lattice work and both candidate validation worlds share one aggregate bone budget before a sentinel pose is evaluated");
        var curveKeys=Enumerable.Range(0,1900).Select(i=>new Key(i/1899d,0)).ToArray();
        var collocations=Prepare(source,curveTarget,Sentinel(2,curveKeys));
        check(collocations.Value is null&&collocations.Diagnostics.Any(d=>d.Code=="RETARGET_UNSUPPORTED"&&d.Message.Contains("curve-collocation resource budget before sampling",StringComparison.Ordinal)),
            "Many native subframe knots on a wide destination graph reject bounded adaptive work before an invalid source-pose sentinel is sampled");
        bool cancelled=false;
        try{Prepare(wide,denseTarget,Sentinel(18000),new CancellationToken(true));}
        catch(OperationCanceledException){cancelled=true;}
        check(cancelled,"Pre-cancelled dense/unequal-graph preparation throws cancellation before resource rejection or the invalid sampled pose");
    }
    static FighterProject Fixture(string prefix,float size)
    {
        var bones=new Bone[]{new(prefix+"_route",-1,Transform.Identity),new(prefix+"_lower",0,Transform.Identity with{Translation=new(0,2*size,0)}),
            new(prefix+"_upper",1,Transform.Identity with{Translation=new(0,size,0)}),new(prefix+"_detail",1,Transform.Identity with{Translation=new(size,0,0)})};
        var vertices=new[]{new Vector3(.2f,.1f,0),new(.1f,.3f,0),new(.1f,.1f,.2f)}.Select(p=>
            new Vertex(new Vector3(size,2*size,0)+p*size,Vector3.UnitZ,Vector2.Zero,new(1,3,0,0),new(.25f,.75f,0,0),Vector4.One)).ToArray();
        var scene=new Scene(prefix,bones,[new("Original weighted triangle",vertices,[0,1,2])]);
        var rig=new ControlRigSetup(Guid.NewGuid(),AnimationControls.Fingerprint(scene),[],[])
        {
            BodyBone=1,Axes=new(Vector3.UnitX,Vector3.UnitY,Vector3.UnitZ),Coordinates=new(1,"Independent model units"),
            BoneRoles=[new("RootMotion",0,"Supplied actor",true),new("Hips",1,"Supplied body",true),new("Chest",2,"Supplied upper body",true),new("Channel.IndependentDetail",3,"Supplied detailed native channel",true)]
        };
        return ProceduralFixture.Create() with{Scene=scene,Clips=[],ControlRigs=[rig],Contacts=[],MotionCoverage=[]};
    }
    static Curve?[] Channels()=>new Curve?[9];
    static Curve PolynomialCurve(Func<double,double> value,Func<double,double> slope)=>new(Enumerable.Range(0,6).Select(k=>new Key(k,(float)value(k),(float)slope(k),(float)slope(k))).ToArray());
    static double Route(double t)=>.03*t*t*t-.11*t*t+.4*t;
    static double RouteDerivative(double t)=>.09*t*t-.22*t+.4;
    static double Lower(double t)=>.02*t*t*t-.07*t*t-.1*t;
    static double LowerDerivative(double t)=>.06*t*t-.14*t-.1;
    static double Reach(double t)=>.01*t*t*t+.02*t*t+.05*t;
    static double ReachDerivative(double t)=>.03*t*t+.04*t+.05;
    static double Stretch(double t)=>1+.03*t*t*t-.05*t*t+.1*t;
    static double StretchDerivative(double t)=>.09*t*t-.1*t+.1;
}
