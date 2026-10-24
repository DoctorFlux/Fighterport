namespace FighterPort.Core;

public record FreeChannelRecoveryReceipt(string Version,int PoseEvaluations,int RecoveredBones,int RecoveredKeys,bool BudgetExhausted,string Coverage);
internal record FreeChannelRecoveryFit(BoneTrack[] Tracks,FreeChannelRecoveryReceipt Receipt);

// Constraint ownership is time-dependent. A correction needed before release
// must not become the new animation of an unrelated/free native branch. Recover
// source-driven coefficients only outside the full interpolation support of
// every related required interaction. Actual skin/limits guard the proposal:
// absence of an explicit pin does not imply absence of collision constraints.
internal static class FreeChannelRecovery
{
    public const string Version="constraint-owned-native-channel-recovery-2";
    sealed class BudgetReached:Exception {}
    public static FreeChannelRecoveryFit Recover(TargetMotionPlan plan,BoneTrack[] input,BoneTrack[] baseline,CancellationToken cancellation,int budget)
    {
        int evaluations=0,recoveredBones=0,recoveredKeys=0;var current=input;
        FreeChannelRecoveryFit Result(bool exhausted)=>new(current,new(Version,evaluations,recoveredBones,recoveredKeys,exhausted,
            "Source-driven local channels outside related required intervals; per-measurement nonregression at complete eighth/contact-boundary samples. Not continuous-time or native-output acceptance."));
        if(budget<1)return Result(false);
        void Charge(){cancellation.ThrowIfCancellationRequested();if(evaluations>=budget)throw new BudgetReached();evaluations++;}
        var scene=plan.Rig.Scene;int keys=input[0].Channels[0]!.Keys.Length;
        var frames=Enumerable.Range(0,(keys-1)*8+1).Select(i=>i/8d)
            .Concat(plan.Graph.Relations.SelectMany(c=>new[]{c.Frame,c.EndFrame??c.Frame}).SelectMany(f=>new[]{f-1d/1024,f,f+1d/1024}).Where(f=>f>=0&&f<=keys-1)).Distinct().Order().ToArray();
        var clip=new AnimationClip(Guid.NewGuid(),"Source-driven free channels",plan.Samples,plan.Rate,plan.Loop,input);
        EvaluatedPose Evaluate(BoneTrack[] tracks,double frame){Charge();return AnimationService.Evaluate(scene,clip with{Tracks=tracks},frame) with{Seconds=frame/plan.Rate.FramesPerSecond};}
        bool Related(int bone,ContactPin pin)
        {
            bool Affects(int other)=>PoseGeometry.Ancestor(scene,bone,other);
            bool Skin(SkinSurfacePoint p)=>Enumerable.Range(0,4).Any(i=>p.Weights[i]>0&&Affects((int)p.Joints[i]));
            return Affects(pin.Bone)||pin.SurfacePoints.Any(Skin)||pin.SurfaceRegion.Any(Skin)||
                pin.Reference?.Bone is {} attachment&&Affects(attachment)||(pin.Reference?.SurfaceTripod??[]).Any(Skin);
        }
        try
        {
            var violations=frames.Select(f=>TargetMotionConstraints.MaximumNormalizedViolation(plan,f,Evaluate(current,f))).ToArray();
            for(int sweep=0;sweep<2;sweep++)
            {
                bool changed=false;
                foreach(int bone in Enumerable.Range(0,scene.Bones.Length).OrderBy(b=>plan.Rig.CanonicalBoneRanks[b]))
                {
                    cancellation.ThrowIfCancellationRequested();if(plan.Rig.IsActorAncestor(bone))continue;
                    int track=Array.FindIndex(current,t=>t.Bone==scene.Bones[bone].Name);if(track<0)continue;
                    var related=plan.Graph.Relations.Where(c=>c.Pins.Any(p=>(!p.SoftSupport||p.RequiredSupport)&&Related(bone,p))).ToArray();
                    var candidate=current.ToArray();var channels=current[track].Channels.ToArray();int changedKeys=0;
                    for(int ch=3;ch<9;ch++)
                    {
                        var curve=channels[ch]!;var expected=baseline[track].Channels[ch]!;var values=curve.Keys.ToArray();
                        for(int k=0;k<keys;k++)
                        {
                            // Both the value and shared derivative affect the
                            // adjacent segments, including the exact endpoint.
                            double start=Math.Max(0,k-1),end=Math.Min(keys-1,k+1);
                            if(related.Any(c=>c.Frame<=end&&(c.EndFrame??c.Frame)>=start))continue;
                            if(values[k]==expected.Keys[k])continue;values[k]=expected.Keys[k];changedKeys++;
                        }
                        channels[ch]=curve with{Keys=values};
                    }
                    if(changedKeys==0)continue;
                    candidate[track]=current[track] with{Channels=channels};
                    var evaluated=frames.Select(f=>Evaluate(candidate,f)).ToArray();
                    if(evaluated.Any(p=>(plan.Controls.JointLimits??[]).Any(l=>Enumerable.Range(0,3).Any(a=>p.Local[l.Bone].Rotation[a]<l.MinimumDegrees[a]-.0001f||p.Local[l.Bone].Rotation[a]>l.MaximumDegrees[a]+.0001f))))continue;
                    var next=frames.Select((f,i)=>TargetMotionConstraints.MaximumNormalizedViolation(plan,f,evaluated[i])).ToArray();
                    if(next.Any(v=>!double.IsFinite(v))||next.Max()>Math.Max(1,violations.Max())+1e-9||next.Where((v,i)=>violations[i]<=1&&v>1).Any())continue;
                    // Recovery is optional. It cannot trade a previously reduced
                    // skin/contact error for a worse one hidden beneath another
                    // frame's maximum, or another constraint in the same pose.
                    if(frames.Where((f,i)=>!TargetMotionConstraints.PreservesRequiredMeasurements(plan,f,Evaluate(current,f),evaluated[i])).Any())continue;
                    current=candidate;violations=next;recoveredBones++;recoveredKeys+=changedKeys;changed=true;
                }
                if(!changed)break;
            }
        }
        catch(BudgetReached){return Result(true);}
        return Result(false);
    }
}
