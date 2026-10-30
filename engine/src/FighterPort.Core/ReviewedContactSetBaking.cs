using System.Numerics;
using System.Text.Json;

namespace FighterPort.Core;

public static partial class ReviewedContactSets
{
    public static ReviewedContactSetResult Prepare(FighterProject project,AnimationClip master,ControlRigSetup rig,
        ReviewedContactContext evidence,CancellationToken cancellation=default,Action<string>? progress=null)
    {
        // Freeze arrays as well as record owners before any offline work.
        var context=JsonSerializer.Deserialize<ReviewedContactContext>(JsonSerializer.Serialize(evidence,ProjectStore.Json),ProjectStore.Json)!;
        ValidateContext(project.Scene,master,rig,context,true);
        if(ContentKey(context.OriginalContacts)!=ContentKey(project.Contacts.Where(c=>c.Clip==master.Id).ToArray()))
            throw new InvalidDataException("The original contact context changed; saved pins cannot be dropped or replaced by suggestions.");
        if(project.NativeLayers.Any(l=>l.Kind!="SCN0"))throw new NotSupportedException("Composed companion channels require synchronized contact verification.");
        if(project.AuthoringContacts.Length>0||project.AuthoringPose.Count>0)throw new NotSupportedException("Bake or review the uncommitted authoring pose/pins before native contact correction.");
        if(context.OriginalContacts.SelectMany(c=>c.Pins).Any(p=>p.Reference is not null||p.EditorMotion is not null||p.EdgeSupport||p.Socket is not null||p.AllowedSurface?.Edge is not null||p.Clearance?.Edge is not null||p.EditorBehavior==EditorPinBehavior.SoftReach||p.SoftSupport))
            throw new NotSupportedException("Moving or ledge support context cannot be replaced by static ground correction.");
        var scene=project.Scene;var rows=context.Ranges.Where(r=>r.Reviewed).ToArray();
        var protectedIndices=AnimationControls.ProtectedMotionBones(scene,rig).Concat(rig.PreservedHelperBones)
            .Concat(context.ProtectedBones.Select(name=>Array.FindIndex(scene.Bones,b=>b.Name==name))).ToHashSet();
        var allowed=rows.SelectMany(row=>{var chain=rig.Chains.Single(c=>c.Reviewed&&c.Effector==row.Bone);return new[]{chain.Root,chain.Middle,chain.Effector};}).ToHashSet();
        allowed.UnionWith(rows.SelectMany(r=>ReviewedContactShapeBones(scene,rig,r)));
        int body=ReviewedContactBody(scene,rig);
        if(body>=0)allowed.Add(body);
        allowed.ExceptWith(protectedIndices);
        if(rows.Any(r=>!allowed.Contains(r.Bone)))throw new InvalidOperationException("An approved contact conflicts with a protected support or motion control. No animation changed.");
        int contactBegin=rows.Min(r=>r.Start-r.Blend),begin=context.EarlierBodyCorrectionStart??contactBegin,end=rows.Max(r=>r.End+r.Blend);
        if(end-begin>MaximumEditedSamples)throw new InvalidOperationException("The contact set exceeds the existing 240-sample edit window.");
        if(context.EarlierBodyCorrectionStart is not null&&context.OriginalContacts.Any(c=>c.Frame<=contactBegin&&(c.EndFrame??c.Frame)>=begin&&c.Pins.Length>0))
            throw new NotSupportedException("The earlier body range overlaps authored pins. Review their release through the native pin workflow first; earlier recovery cannot override them.");
        if(context.ProtectedIntervals.Any(p=>begin<p.End&&end>p.Start)||context.EarlierBodyCorrectionStart is null&&context.FittingMiddle is {} middle&&begin<middle.End&&end>middle.Start)
            throw new InvalidOperationException("A reviewed contact overlaps the retained fitting/action middle. Adjust the recovery start or contact range; protected data cannot be released by a suggestion.");
        if(project.Moves.SelectMany(m=>m.Events).Any(e=>e.Start<=end&&e.End>=begin))
            throw new InvalidOperationException("Contact correction overlaps a protected gameplay event.");
        var anatomy=RigCharacterization.Describe(scene,rig);var normal=new Vector3(anatomy.Basis.M21,anatomy.Basis.M22,anatomy.Basis.M23);
        float positionBound=rows.Select(r=>{var calibration=rig.Chains.Single(c=>c.Reviewed&&c.Effector==r.Bone).Calibration;
            return calibration is null?throw new InvalidDataException("Review finite limb calibration first."):calibration.UpperLength+calibration.LowerLength;}).Min()*.001f;
        if(!float.IsFinite(positionBound)||positionBound<=0)throw new InvalidDataException("Contact bounds need a finite positive reviewed limb scale.");
        const float orientationBound=.25f;
        // A sole witness must remain within the drift bound of its frozen
        // anchor AND above floor minus the penetration bound. Anchors more
        // than two bounds below the floor make those requirements contradictory;
        // no amount of temporal refinement or IK can satisfy them.
        foreach(var row in rows)
        {
            var complete=FullFootWitnesses(scene,master,row,normal);
            if(complete.Anchors.Any(point=>Vector3.Dot(point,normal)<context.Surface.SourceFloor!.Value-2*positionBound))
                throw new InvalidOperationException("The frozen full-sole anchor contradicts the reviewed floor within the unchanged contact bounds. Review the anchor and complete skin geometry; no candidate is available.");
            foreach(int boundary in new[]{row.Start-row.Blend,row.End+row.Blend})
            {
                if(EarlierBodyActive(context,boundary)||rows.Any(other=>boundary>other.Start-other.Blend&&boundary<other.End+other.Blend))continue;
                // Existing paired sides can describe an authored jump; leave
                // those to the complete native boundary verifier instead.
                if(master.Tracks.SelectMany(t=>t.Channels.OfType<Curve>()).Any(c=>c.Keys.Count(k=>k.Frame==boundary)>1))continue;
                var pose=ContactPose(scene,master,boundary);
                float depth=context.Surface.SourceFloor!.Value-complete.Region.Min(p=>Vector3.Dot(ContactGeometry.Point(p,pose),normal));
                if(depth>positionBound)
                    throw new InvalidOperationException(row.Role+" has a fixed penetrated contact boundary at native display frame "+(boundary+1)+" (depth "+depth+", limit "+positionBound+"). A continuous floor recovery cannot retain that boundary and immediately meet the same clearance bound. Review the range/fitting or support context; no candidate is available.");
            }
        }
        // Analytic bend-plane initialization is ill-conditioned at a straight
        // reviewed anchor. Freeze this geometric policy for the complete range;
        // never switch it at a per-sample threshold or infer it from bone names.
        bool analyticSubframes=rows.All(row=>
        {
            var chain=rig.Chains.Single(c=>c.Reviewed&&c.Effector==row.Bone);var pose=ContactPose(scene,master,row.AnchorFrame);
            var upper=pose.World[chain.Middle].Translation-pose.World[chain.Root].Translation;
            var lower=pose.World[chain.Effector].Translation-pose.World[chain.Middle].Translation;
            double denominator=(double)upper.LengthSquared()*lower.LengthSquared();
            return denominator>0&&Vector3.Cross(upper,lower).LengthSquared()/denominator>=1e-6;
        });
        var solverRegions=rows.ToDictionary(r=>r.Id,r=>ContactSolverRegion(scene,r),StringComparer.Ordinal);
        var completeGeometry=rows.ToDictionary(r=>r.Id,r=>FullFootWitnesses(scene,master,r,normal),StringComparer.Ordinal);
        long proposalSkinWork=0;int independentlyAcceptedTrials=0;
        int evaluations=0;string refinement="before first curve build";
        var solved=new SortedDictionary<double,Transform[]>();
        var originalPoses=new Dictionary<double,EvaluatedPose>();
        var temporalSeeds=new Dictionary<int,Queue<(double Frame,Transform[] Local)>>();
        EvaluatedPose OriginalAt(double frame)
        {
            if(!originalPoses.TryGetValue(frame,out var value)){value=ContactPose(scene,master,frame);originalPoses.Add(frame,value);}return value;
        }
        Transform[] Remember(double frame,Transform[] values)
        {
            solved.Add(frame,values);
            if(frame>=contactBegin&&frame<end)
            {
                int cell=(int)Math.Floor(frame);if(!temporalSeeds.TryGetValue(cell,out var queue)){queue=new();temporalSeeds.Add(cell,queue);}
                queue.Enqueue((frame,values));if(queue.Count>64)queue.Dequeue();
            }
            return values;
        }
        float Weight(ReviewedContactRange row,double frame)
        {
            float t=frame<row.Start?(float)((frame-(row.Start-row.Blend))/row.Blend):frame>row.End?(float)((row.End+row.Blend-frame)/row.Blend):1;
            t=Math.Clamp(t,0,1);return t*t*(3-2*t);
        }
        ContactPin[] Goals(double frame,EvaluatedPose original)
        {
            var pins=new List<ContactPin>(context.OriginalContacts.Where(c=>c.Contains(frame)).SelectMany(c=>c.Pins));
            foreach(var row in rows)
            {
                float weight=Weight(row,frame);if(weight<=0&&!(context.EarlierBodyCorrectionStart is not null&&frame==contactBegin&&row.Start-row.Blend==contactBegin))continue;
                if(pins.Any(p=>p.Bone==row.Bone))throw new InvalidOperationException("An existing contact overlaps the reviewed replacement. Release or review that original constraint explicitly.");
                pins.Add(ContactGoal(scene,master,rig,context,row,frame,original,normal,weight,solverRegions[row.Id]));
                if(weight>=1)for(int point=0;point<row.Patch.Length;point++)
                    pins.Add(new(Guid.ParseExact(ContentKey(new{Range=row.Id,Point=point})[..32],"N"),row.Role,row.Bone,Vector3.Zero,row.AnchorPoints[point],row.Orientation,false,"Reviewed sole patch")
                        {SurfacePoints=[row.Patch[point]]});
            }
            return pins.ToArray();
        }
        bool ReviewedTrial(EvaluatedPose pose,EvaluatedPose original,IReadOnlyDictionary<int,Transform> locks,ContactPin[] goals,double frame)
        {
            if(pose.Local.Any(t=>t.Channels().Any(v=>!float.IsFinite(v)))||locks.Any(p=>pose.Local[p.Key]!=p.Value)||
                pose.Local.Where((value,i)=>value.Scale!=original.Local[i].Scale).Any())return false;
            foreach(var pin in goals)
            {
                float distance=ContactGeometry.Residual(pin,pose).Length(),depth=ContactGeometry.Penetration(pin,pose),surface=ContactGeometry.AllowedSurfaceResidual(pin,pose).Length();
                if(!float.IsFinite(distance)||!float.IsFinite(depth)||!float.IsFinite(surface)||distance>positionBound||depth>positionBound||surface>positionBound||
                    pin.HoldRotation&&ContactAuthoring.AngleError(PoseGeometry.Rotation(pose.World[pin.Bone]),ContactGeometry.TargetRotation(pin,pose))>orientationBound)return false;
            }
            foreach(var row in rows.Where(r=>Weight(r,frame)>0))
            {
                var geometry=completeGeometry[row.Id];proposalSkinWork+=geometry.Region.Length+geometry.Sole.Length;
                if(proposalSkinWork>MaximumKeys)throw new InvalidOperationException("Contact trial verification exhausted the unchanged full-skin work budget.");
                if(geometry.Region.Any(p=>context.Surface.SourceFloor!.Value-Vector3.Dot(ContactGeometry.Point(p,pose),normal)>positionBound))return false;
                if(frame>=row.Start&&frame<=row.End&&geometry.Sole.Where((p,i)=>Vector3.Distance(ContactGeometry.Point(p,pose),geometry.Anchors[i])>positionBound).Any())return false;
            }
            return true;
        }
        Transform[]? AcceptReviewedTrial(EvaluatedPose? trial,EvaluatedPose original,IReadOnlyDictionary<int,Transform> locks,ContactPin[] goals,double frame)
        {
            if(trial is null)return null;
            if(!ReviewedTrial(trial,original,locks,goals,frame))
            {
                var adjusted=BodyClearanceTrial(scene,rig,context,trial,locks,goals,frame,positionBound,normal,completeGeometry,ref proposalSkinWork);
                if(adjusted is null||!ReviewedTrial(adjusted,original,locks,goals,frame))return null;
                trial=adjusted;
            }
            independentlyAcceptedTrials++;return trial.Local.ToArray();
        }
        Transform[] SolveAt(double frame)
        {
            cancellation.ThrowIfCancellationRequested();
            if(solved.TryGetValue(frame,out var prior))return prior;
            if(++evaluations>EvaluationBudget)throw new InvalidOperationException("Contact set exhausted its fixed offline pose budget ("+refinement+"). No candidate is available.");
            var original=OriginalAt(frame);
            if(context.EarlierBodyCorrectionStart is not null&&frame<contactBegin&&frame>begin)
            {
                var entry=SolveAt(contactBegin);var earlier=EarlierBodyPose(scene,master,rig,context,frame,original,entry,contactBegin);
                return Remember(frame,earlier);
            }
            bool entryTarget=context.EarlierBodyCorrectionStart is not null&&frame==contactBegin;
            if(!entryTarget&&rows.All(r=>Weight(r,frame)<=0))return Remember(frame,original.Local.ToArray());
            var locks=Enumerable.Range(0,scene.Bones.Length).Where(i=>!allowed.Contains(i)).ToDictionary(i=>i,i=>original.Local[i]);
            foreach(int bone in OriginalProtection(scene,context,frame,rig))locks[bone]=original.Local[bone];
            foreach(var row in rows.Where(r=>Weight(r,frame)>0))
            {
                var anchor=ContactPose(scene,master,row.AnchorFrame);float weight=Weight(row,frame);
                foreach(int bone in ReviewedContactShapeBones(scene,rig,row).Where(b=>allowed.Contains(b)&&!locks.ContainsKey(b)))
                {
                    var a=original.Local[bone];var b=anchor.Local[bone];if(a.Rotation==b.Rotation){locks[bone]=a;continue;}
                    var rotation=Quaternion.Slerp(PoseGeometry.Rotation(a.Matrix),PoseGeometry.Rotation(b.Matrix),weight);
                    locks[bone]=a with{Rotation=AnimationControls.Euler(Matrix4x4.CreateFromQuaternion(rotation),a.Rotation)};
                }
            }
            var goals=Goals(frame,original);
            // Use the same reviewed geometric branch throughout the range.
            // Switching between an original-pose solution and an anchor-seeded
            // solution only when a neighboring solve fails can introduce
            // numerical branch jumps into an otherwise smooth trajectory.
            // Fidelity is still measured against the original finished pose.
            var solvedInput=ContactAnchorSeed(scene,master,rig,rows,frame,original,locks,goals);
            EvaluatedPose fidelity=original;bool temporal=false;
            if(frame!=Math.Floor(frame))
            {
                double first=Math.Floor(frame),last=Math.Ceiling(frame);float fraction=(float)(frame-first);
                var a=SolveAt(first);var b=SolveAt(last);
                var originalA=OriginalAt(first);var originalB=OriginalAt(last);
                var seedValues=original.Local.Select((value,i)=>(value,i)).ToDictionary(p=>p.i,p=>p.value);
                foreach(int bone in allowed.Where(bone=>!locks.ContainsKey(bone)))
                {
                    var da=a[bone].Rotation-originalA.Local[bone].Rotation;
                    // Carry the correction, keeping the master's native Euler
                    // winding/time behavior rather than normalizing its turns.
                    var rb=ContactEulerNear(b[bone].Rotation,originalB.Local[bone].Rotation+da);
                    var db=rb-originalB.Local[bone].Rotation;
                    seedValues[bone]=original.Local[bone] with{
                        Rotation=original.Local[bone].Rotation+Vector3.Lerp(da,db,fraction),
                        Translation=original.Local[bone].Translation+Vector3.Lerp(a[bone].Translation-originalA.Local[bone].Translation,b[bone].Translation-originalB.Local[bone].Translation,fraction)};
                }
                fidelity=AnimationService.Evaluate(scene,null,0,seedValues);
                var nearest=frame-first<=last-frame?(Frame:first,Local:a):(Frame:last,Local:b);
                if(temporalSeeds.TryGetValue((int)first,out var queue))foreach(var item in queue)
                    if(Math.Abs(item.Frame-frame)<Math.Abs(nearest.Frame-frame))nearest=item;
                solvedInput=ContactTemporalSeed(scene,original,OriginalAt(nearest.Frame),nearest.Local,allowed,locks);temporal=true;
            }
            else if(frame>contactBegin)
            {
                var previous=SolveAt(frame-1);
                solvedInput=ContactTemporalSeed(scene,original,OriginalAt(frame-1),previous,allowed,locks);temporal=true;
            }
            // Native integer poses establish the original-pose-fidelity path.
            // Subframes project its continuous correction onto the SAME hard
            // contact constraints. Re-minimizing the original pose independently
            // at every subframe produces noisy redundant-joint optima, rather
            // than a temporal trajectory that any native curve can represent.
            EvaluatedPose? rejected=null;
            var result=ContactSolver.Solve(scene,solvedInput,rig,locks,goals,adjustBody:true,referencePose:fidelity,preserveFeasibleSeed:true,initializeChains:!temporal&&analyticSubframes,onRejectedTrial:p=>rejected=p);
            if(result.Value is null&&result.Diagnostics.Any(d=>d.Code=="CONTACT_UNRESOLVED"))
            {
                cancellation.ThrowIfCancellationRequested();
                if(++evaluations>EvaluationBudget)throw new InvalidOperationException("Contact seed search exhausted its unchanged offline budget.");
                solvedInput=entryTarget?ContactAnchorSeed(scene,master,rig,rows,frame,original,locks,goals,true):original;
                // A retry may change its starting pose, never its fidelity
                // objective. Switching back to the uncorrected master only
                // for failed subframes makes the trajectory discontinuous.
                result=ContactSolver.Solve(scene,solvedInput,rig,locks,goals,adjustBody:true,referencePose:fidelity,onRejectedTrial:p=>rejected=p);
            }
            var local=result.Value is null?AcceptReviewedTrial(rejected,original,locks,goals,frame):solvedInput.Local.ToArray();
            if(local is null)throw new InvalidOperationException("Contact conflict at native frame "+(frame+1)+": "+string.Join("; ",result.Diagnostics.Select(d=>d.Message)));
            foreach(var edit in result.Value?.Transforms??new Dictionary<int,Transform>())
            {
                if(!allowed.Contains(edit.Key)&&edit.Value!=original.Local[edit.Key])throw new InvalidDataException("Contact solver attempted to change a protected native dependency.");
                local[edit.Key]=edit.Value;
            }
            if(local.Where((value,i)=>!allowed.Contains(i)&&value!=original.Local[i]).Any())throw new InvalidDataException("Contact seed changed an unreviewed native dependency.");
            foreach(int bone in allowed.Where(b=>!locks.ContainsKey(b)))local[bone]=local[bone] with{Rotation=ContactEulerNear(local[bone].Rotation,original.Local[bone].Rotation)};
            return Remember(frame,local);
        }
        var times=new SortedSet<double>(Enumerable.Range(begin,end-begin+1).Select(i=>(double)i));
        if(context.EarlierBodyCorrectionStart is not null)
        {
            var editableNames=allowed.Select(b=>scene.Bones[b].Name).ToHashSet(StringComparer.Ordinal);
            times.UnionWith(master.Tracks.Where(t=>editableNames.Contains(t.Bone)).SelectMany(t=>t.Channels.OfType<Curve>())
                .SelectMany(c=>c.Keys.GroupBy(k=>k.Frame).Where(g=>g.Count()>1).Select(g=>g.Key)).Where(t=>t>begin&&t<=contactBegin));
            if(times.Count>EvaluationBudget)throw new InvalidOperationException("Authored paired boundaries exceed the fixed contact work budget.");
        }
        var tracksByName=master.Tracks.ToDictionary(t=>t.Bone,StringComparer.Ordinal);
        var working=new AnimationClip(master.Id,master.Name,master.SampleCount,master.Rate,master.Loop,master.Tracks){NativeWrite=NativeWriteMode.NewResource};
        var tangents=new Dictionary<(int Bone,int Channel,double Frame),float>();
        BoneTrack[] Build()
        {
            var changed=new List<BoneTrack>();
            var earlierEntry=context.EarlierBodyCorrectionStart is null?null:SolveAt(contactBegin);
            var entryOriginal=earlierEntry is null?null:ContactPose(scene,master,contactBegin);
            foreach(int bone in allowed.Order())
            {
                var name=scene.Bones[bone].Name;var baseline=tracksByName.GetValueOrDefault(name)??new(name,new Curve?[9]);
                if(baseline.Rotation is not null)throw new NotSupportedException("Native reviewed contacts require Euler channels.");
                var channels=baseline.Channels.ToArray();bool differs=false;
                for(int channel=3;channel<9;channel++)
                {
                    var curve=channels[channel];
                    if(curve is not null&&(curve.Interpolation!=Interpolation.Hermite||curve.LoopFrameLimit is not null)||master.Loop)
                        throw new NotSupportedException("Reviewed contact edits preserve stepped/looped channels until their boundary adapter is qualified.");
                    if(curve?.Keys.Where(k=>k.Frame>begin&&k.Frame<end).GroupBy(k=>k.Frame).Any(g=>g.Count()>1&&(earlierEntry is null||g.Key>contactBegin))==true)
                        throw new NotSupportedException("Reviewed contact correction cannot smooth an authored paired-key boundary inside its edit window.");
                    var channelTimes=times.Where(t=>earlierEntry is null||t>=contactBegin||t==begin)
                        .Concat(earlierEntry is null?[]:(curve?.Keys??[]).Where(k=>k.Frame>begin&&k.Frame<contactBegin).Select(k=>k.Frame)).Distinct().Order().ToArray();
                    var samples=channelTimes.Select(t=>(Time:t,Value:SolveAt(t)[bone].Channels()[channel])).ToArray();
                    if(samples.All(s=>BitConverter.SingleToInt32Bits(s.Value)==BitConverter.SingleToInt32Bits(ContactLocal(scene.Bones[bone],tracksByName.GetValueOrDefault(name),s.Time).Channels()[channel])))continue;
                    differs=true;
                    var keys=(curve?.Keys??[]).Where(k=>k.Frame<begin||k.Frame>end).ToList();
                    for(int sampleIndex=0;sampleIndex<samples.Length;sampleIndex++)
                    {
                        var sample=samples[sampleIndex];
                        var authored=curve?.Keys.Where(k=>k.Frame==sample.Time).ToArray()??[];
                        if(earlierEntry is not null&&sample.Time>begin&&sample.Time<=contactBegin&&(sample.Time<contactBegin||authored.Length>1))
                        {
                            float u=(float)((sample.Time-begin)/(contactBegin-begin)),weight=u*u*(3-2*u),slopeWeight=6*u*(1-u)/(contactBegin-begin);
                            var deltaR=ContactEulerNear(earlierEntry[bone].Rotation,entryOriginal!.Local[bone].Rotation)-entryOriginal.Local[bone].Rotation;
                            var deltaT=earlierEntry[bone].Translation-entryOriginal.Local[bone].Translation;
                            float delta=channel<6?deltaR[channel-3]:deltaT[channel-6];
                            float offset=delta*weight,derivative=delta*slopeWeight;
                            var originalKeys=authored.Length>0?authored:[new Key(sample.Time,curve is null?scene.Bones[bone].Rest.Channels()[channel]:NativeContactSample(curve,sample.Time),BoundaryFitting.Derivative(curve,sample.Time),BoundaryFitting.Derivative(curve,sample.Time))];
                            foreach(var key in originalKeys)keys.Add(key with{Value=offset==0?key.Value:key.Value+offset,
                                InTangent=derivative==0?key.InTangent:key.InTangent+derivative,OutTangent=derivative==0?key.OutTangent:key.OutTangent+derivative});
                            continue;
                        }
                        float slope;
                        if(sample.Time==begin||sample.Time==end)slope=BoundaryFitting.Derivative(curve,sample.Time);
                        else
                        {
                            if(!tangents.TryGetValue((bone,channel,sample.Time),out slope))
                            {
                                slope=(samples[sampleIndex+1].Value-samples[sampleIndex-1].Value)/(float)(samples[sampleIndex+1].Time-samples[sampleIndex-1].Time);
                                tangents.Add((bone,channel,sample.Time),slope);
                            }
                        }
                        if(!float.IsFinite(slope))throw new InvalidDataException("Contact curve tangent is nonfinite.");
                        if(sample.Time==begin||sample.Time==end)
                        {
                            // Retain both authored sides, including signed zero.
                            if(authored.Length>0){keys.AddRange(authored);continue;}
                        }
                        keys.Add(new(sample.Time,sample.Value,slope,slope));
                    }
                    channels[channel]=new(keys.OrderBy(k=>k.Frame).ToArray());
                }
                if(differs)changed.Add(baseline with{Channels=channels});
            }
            if(changed.Sum(t=>(long)t.Channels.OfType<Curve>().Sum(c=>c.Keys.Length))>MaximumKeys)throw new InvalidOperationException("Contact candidate exceeds the native-key budget.");
            working=working with{Tracks=master.Tracks.Where(t=>changed.All(c=>c.Bone!=t.Bone)).Concat(changed).ToArray()};
            return changed.ToArray();
        }
        BoneTrack[] changed=[];
        for(int sweep=0;sweep<12;sweep++)
        {
            progress?.Invoke("Building sweep "+(sweep+1)+", "+times.Count+" knots, "+evaluations+" evaluations; previous "+refinement);
            cancellation.ThrowIfCancellationRequested();changed=Build();
            var failures=new SortedSet<double>();int contactFailures=0,patchFailures=0,poseFailures=0;float worstPose=0;double worstFrame=0;string worstBone="";
            var knots=times.ToArray();
            foreach(double frame in knots)
            {
                var actual=ContactPose(scene,working,frame);var expected=AnimationService.Evaluate(scene,null,0,SolveAt(frame).Select((value,i)=>(value,i)).ToDictionary(p=>p.i,p=>p.value));
                if(allowed.Any(b=>Vector3.Distance(actual.World[b].Translation,expected.World[b].Translation)>positionBound))
                    throw new InvalidDataException("Native contact keys do not reproduce the prepared pose at display frame "+(frame+1)+".");
            }
            for(int i=0;i<knots.Length-1;i++)foreach(double fraction in new[]{.25,.5,.75})
            {
                double frame=knots[i]+(knots[i+1]-knots[i])*fraction;
                if(++evaluations>EvaluationBudget)throw new InvalidOperationException("Contact native-curve verification exhausted its fixed pose budget (sweep "+(sweep+1)+", "+times.Count+" knots). No candidate is available.");
                var actual=ContactPose(scene,working,frame);
                var original=ContactPose(scene,master,frame);
                bool failed=Goals(frame,original).Any(pin=>ContactGeometry.Residual(pin,actual).Length()>positionBound||ContactGeometry.Penetration(pin,actual)>positionBound||
                    pin.HoldRotation&&ContactAuthoring.AngleError(PoseGeometry.Rotation(actual.World[pin.Bone]),pin.Rotation)>orientationBound);
                if(failed)contactFailures++;
                bool patchFailed=rows.Where(r=>frame>=r.Start&&frame<=r.End).Any(row=>row.Patch.Where((p,j)=>Vector3.Distance(ContactGeometry.Point(p,actual),row.AnchorPoints[j])>positionBound).Any());
                if(patchFailed)patchFailures++;failed|=patchFailed;
                // The pre-contact additive correction is a fixed analytic
                // reference. During contact, validate the authored curve's
                // hard constraints rather than re-solving an underdetermined
                // knee/pelvis optimum as an interpolation reference.
                bool poseFailed=false;
                if(context.EarlierBodyCorrectionStart is not null&&frame<contactBegin)
                {
                    var expected=AnimationService.Evaluate(scene,null,0,EarlierBodyPose(scene,master,rig,context,frame,original,SolveAt(contactBegin),contactBegin).Select((value,j)=>(value,j)).ToDictionary(p=>p.j,p=>p.value));
                    foreach(int bone in allowed){float delta=Vector3.Distance(actual.World[bone].Translation,expected.World[bone].Translation);if(delta>worstPose){worstPose=delta;worstBone=scene.Bones[bone].Name;worstFrame=frame;}}
                    poseFailed=allowed.Any(b=>Vector3.Distance(actual.World[b].Translation,expected.World[b].Translation)>positionBound);
                }
                if(poseFailed)poseFailures++;failed|=poseFailed;
                if(failed)failures.Add(frame);
            }
            refinement="sweep "+(sweep+1)+", knots "+times.Count+", contact "+contactFailures+", patch "+patchFailures+", pose "+poseFailures+", worst pose "+worstPose+" on "+worstBone+" at "+worstFrame;
            progress?.Invoke(refinement);
            if(failures.Count==0)break;
            if(sweep==11)throw new InvalidOperationException("Contact curves did not converge within the fixed refinement limit ("+refinement+").");
            foreach(double frame in failures)
            {
                cancellation.ThrowIfCancellationRequested();
                if(frame<contactBegin&&context.EarlierBodyCorrectionStart is not null){SolveAt(frame);continue;}
                if(++evaluations>EvaluationBudget)throw new InvalidOperationException("Contact projection exhausted the unchanged offline pose budget.");
                var original=OriginalAt(frame);var current=ContactPose(scene,working,frame);
                var locks=Enumerable.Range(0,scene.Bones.Length).Where(b=>!allowed.Contains(b)).ToDictionary(b=>b,b=>original.Local[b]);
                foreach(int bone in OriginalProtection(scene,context,frame,rig))locks[bone]=original.Local[bone];
                var goals=Goals(frame,original);
                EvaluatedPose? trial=null;
                var result=ContactSolver.Solve(scene,current,rig,locks,goals,adjustBody:true,referencePose:current,preserveFeasibleSeed:true,initializeChains:false,onRejectedTrial:p=>trial=p);
                if(result.Value is null)
                {
                    if(++evaluations>EvaluationBudget)throw new InvalidOperationException("Contact projection retry exhausted the unchanged offline pose budget.");
                    result=ContactSolver.Solve(scene,current,rig,locks,goals,adjustBody:true,referencePose:current,preserveFeasibleSeed:true,onRejectedTrial:p=>trial=p);
                }
                Transform[] local;
                if(result.Value is null)
                    local=AcceptReviewedTrial(trial,original,locks,goals,frame)??throw new InvalidOperationException("Native contact interpolation projection failed at display frame "+(frame+1)+": "+string.Join("; ",result.Diagnostics.Select(d=>d.Message)));
                else{local=current.Local.ToArray();foreach(var edit in result.Value.Transforms)local[edit.Key]=edit.Value;}
                foreach(int bone in allowed)
                {
                    if(!locks.ContainsKey(bone))local[bone]=local[bone] with{Rotation=ContactEulerNear(local[bone].Rotation,current.Local[bone].Rotation)};
                    var track=working.Tracks.SingleOrDefault(t=>t.Bone==scene.Bones[bone].Name);
                    for(int channel=3;channel<9;channel++)tangents[(bone,channel,frame)]=BoundaryFitting.Derivative(track?.Channels[channel],frame);
                }
                Remember(frame,local);
            }
            times.UnionWith(failures);
        }
        var witnesses=new SortedSet<double>(times);
        for(double frame=begin;frame<=end;frame+=.25)witnesses.Add(frame);
        foreach(var frame in times.Zip(times.Skip(1),(a,b)=>a+(b-a)*.371))witnesses.Add(frame);
        var metrics=Validate(scene,working,master,rig,context,positionBound,orientationBound,witnesses.ToArray(),cancellation,skinWorkOffset:proposalSkinWork);
        if(MasterKey(scene,master)!=context.MasterKey)throw new InvalidDataException("Contact master changed during preparation.");
        var receipt=new ReviewedContactSetReceipt(Version,ContentKey(context),context.MasterKey,begin,end,changed.Select(t=>t.Bone).ToArray(),positionBound,orientationBound,
            witnesses.ToArray(),metrics.Drift,metrics.Penetration,metrics.Angle,evaluations,new[]{
                context.TrajectoryPolicy,
                "Unconverged internal-margin trial poses independently meeting the unchanged reviewed contract: "+independentlyAcceptedTrials+". Strict solver failure status is retained; native serialization/reopen remains mandatory.",
                context.EarlierBodyCorrectionStart is {} reviewedFirst?context.EarlierCorrectionPolicy+" Reviewed first display frame: "+(reviewedFirst+1):"No earlier body correction was approved; the retained fitting middle stays exact.",
                context.EarlierBodyCorrectionStart is {} early?"Reviewed inferred support dependencies in the body range: "+string.Join(", ",context.InferredSupports.Where(s=>s.First<end&&s.Last>early).Select(s=>s.Role).Distinct()):"Original inferred support locks remain unchanged.",
                "Temporal seeds retain at most 64 samples per native-frame cell; fidelity is always frozen to the original integer path. Analytic IK is used only for cold starts (anchor conditioning threshold 0.001). All acceptance bounds and work limits are unchanged.",
                "Reviewed static foot contacts only. Native encoding and fresh reopen must independently pass before Apply.",
                "Finite integer/quarter/adaptive/withheld geometry evidence is not a continuous-time or game acceptance proof.",
                "All original skin influences participate; protected roots and exterior native channels remain unchanged."});
        var contacts=context.OriginalContacts.Concat(rows.Select(row=>new ContactFrame(Guid.ParseExact(row.Id,"N"),working.Id,row.Start,rig.SkeletonFingerprint,
            [new(Guid.ParseExact(row.Id,"N"),row.Role,row.Bone,Vector3.Zero,row.Anchor,row.Orientation,true,"Reviewed static floor")
            {SurfacePoints=row.Patch,Clearance=new(normal,context.Surface.SourceFloor!.Value,row.Region)}]){EndFrame=row.End})).ToArray();
        return new(working,changed,contacts,context,receipt);
    }

    public static (float Drift,float Penetration,float Angle) Validate(Scene scene,AnimationClip candidate,AnimationClip master,ControlRigSetup rig,
        ReviewedContactContext context,float positionBound,float orientationBound,double[] witnesses,CancellationToken cancellation=default,bool preserveEditScope=true,long skinWorkOffset=0)
    {
        ValidateContext(scene,master,rig,context,true);
        float frozenBound=context.Ranges.Where(r=>r.Reviewed).Select(r=>
        {
            var calibrated=rig.Chains.Single(c=>c.Reviewed&&c.Effector==r.Bone).Calibration??throw new InvalidDataException("Missing reviewed limb scale.");
            return calibrated.UpperLength+calibrated.LowerLength;
        }).Min()*.001f;
        if(!float.IsFinite(positionBound)||positionBound<=0||orientationBound!=.25f||witnesses.Length>EvaluationBudget||witnesses.Any(t=>!double.IsFinite(t)||t<0||t>master.SampleCount-1))
            throw new InvalidDataException("Invalid bounded contact verification request.");
        if(positionBound!=frozenBound)throw new InvalidDataException("Contact verification cannot change the frozen limb-scaled bound.");
        if(candidate.SampleCount!=master.SampleCount||candidate.Rate!=master.Rate||candidate.Loop!=master.Loop)throw new InvalidDataException("Contact candidate changed native timing.");
        var rows=context.Ranges.Where(r=>r.Reviewed).ToArray();var anatomy=RigCharacterization.Describe(scene,rig);
        var solverRegions=rows.ToDictionary(r=>r.Id,r=>ContactSolverRegion(scene,r),StringComparer.Ordinal);
        var normal=new Vector3(anatomy.Basis.M21,anatomy.Basis.M22,anatomy.Basis.M23);float drift=0,penetration=0,angle=0;
        var protectedBones=AnimationControls.ProtectedMotionBones(scene,rig).Concat(rig.PreservedHelperBones).Concat(context.ProtectedBones.Select(n=>Array.FindIndex(scene.Bones,b=>b.Name==n))).Distinct().ToArray();
        var editable=rows.SelectMany(r=>{var chain=rig.Chains.Single(c=>c.Reviewed&&c.Effector==r.Bone);return new[]{chain.Root,chain.Middle,chain.Effector};}).ToHashSet();
        editable.UnionWith(rows.SelectMany(r=>ReviewedContactShapeBones(scene,rig,r)));
        int body=ReviewedContactBody(scene,rig);if(body>=0)editable.Add(body);editable.ExceptWith(protectedBones);
        var beforeTracks=master.Tracks.ToDictionary(t=>t.Bone,StringComparer.Ordinal);
        var afterTracks=candidate.Tracks.ToDictionary(t=>t.Bone,StringComparer.Ordinal);
        foreach(string name in preserveEditScope?beforeTracks.Keys.Concat(afterTracks.Keys).Distinct(StringComparer.Ordinal):Enumerable.Empty<string>())
        {
            int bone=Array.FindIndex(scene.Bones,b=>b.Name==name);
            var before=beforeTracks.GetValueOrDefault(name);var after=afterTracks.GetValueOrDefault(name);
            if(!editable.Contains(bone)&&ContentKey(before)!=ContentKey(after))
                throw new InvalidDataException("Contact candidate changed an unreviewed native track: "+name);
            if(editable.Contains(bone)&&ContentKey(Enumerable.Range(0,3).Select(c=>before?.Channels[c]).ToArray())!=ContentKey(Enumerable.Range(0,3).Select(c=>after?.Channels[c]).ToArray()))
                throw new InvalidDataException("Contact correction cannot change native scale channels.");
        }
        var required=new SortedSet<double>(witnesses);
        int first=context.EarlierBodyCorrectionStart??rows.Min(r=>r.Start-r.Blend),last=rows.Max(r=>r.End+r.Blend);
        for(double frame=0;frame<=master.SampleCount-1;frame+=.25)required.Add(frame);
        foreach(var frame in master.Tracks.Concat(candidate.Tracks).SelectMany(t=>t.Channels.OfType<Curve>()).SelectMany(c=>c.Keys).Select(k=>k.Frame).Where(t=>t>=first&&t<=last))required.Add(frame);
        if(required.Count>EvaluationBudget)throw new InvalidOperationException("Complete native contact witnesses exceed the fixed verification budget.");
        var full=rows.Select(row=>FullFootWitnesses(scene,master,row,normal)).ToArray();long skinWork=skinWorkOffset;
        if(skinWork<0||skinWork>MaximumKeys)throw new InvalidDataException("Invalid shared full-skin verification budget.");
        foreach(double frame in required)
        {
            cancellation.ThrowIfCancellationRequested();var pose=ContactPose(scene,candidate,frame);var baseline=ContactPose(scene,master,frame);
            bool outside=context.EarlierBodyCorrectionStart is not null?frame<=first||frame>=last:rows.All(r=>frame<=r.Start-r.Blend||frame>=r.End+r.Blend);
            if(preserveEditScope&&outside&&pose.Local.Where((t,i)=>t!=baseline.Local[i]).Any())
            {
                int changed=Enumerable.Range(0,scene.Bones.Length).First(i=>pose.Local[i]!=baseline.Local[i]);
                throw new InvalidOperationException("Contact correction changed the original exterior at "+frame+" on "+scene.Bones[changed].Name+": "+pose.Local[changed]+" versus "+baseline.Local[changed]);
            }
            if(preserveEditScope&&protectedBones.Any(b=>pose.Local[b]!=baseline.Local[b]))throw new InvalidOperationException("Contact candidate changed a protected native motion/support control.");
            if(preserveEditScope&&OriginalProtection(scene,context,frame,rig).Any(b=>pose.Local[b]!=baseline.Local[b]))throw new InvalidOperationException("Contact candidate changed an original authored contact dependency.");
            bool fixedInterval=context.ProtectedIntervals.Any(p=>frame>=p.Start&&frame<=p.End)||context.EarlierBodyCorrectionStart is null&&context.FittingMiddle is {} retained&&frame>=retained.Start&&frame<=retained.End;
            if(preserveEditScope&&fixedInterval&&pose.Local.Where((value,i)=>value!=baseline.Local[i]).Any())throw new InvalidOperationException("Contact correction changed an exact protected action/fitting interval.");
            foreach(var pin in context.OriginalContacts.Where(c=>c.Contains(frame)).SelectMany(c=>c.Pins))
            {
                drift=Math.Max(drift,ContactGeometry.Residual(pin,pose).Length());
                penetration=Math.Max(penetration,ContactGeometry.Penetration(pin,pose));
                if(pin.HoldRotation)angle=Math.Max(angle,ContactAuthoring.AngleError(PoseGeometry.Rotation(pose.World[pin.Bone]),pin.Rotation));
            }
            foreach(var row in rows)
            {
                float weight=frame<row.Start?(float)((frame-row.Start+row.Blend)/row.Blend):frame>row.End?(float)((row.End+row.Blend-frame)/row.Blend):1;
                weight=Math.Clamp(weight,0,1);weight=weight*weight*(3-2*weight);
                if(weight<=0)continue;
                var goal=ContactGoal(scene,master,rig,context,row,frame,baseline,normal,weight,solverRegions[row.Id]);
                var actual=row.Patch.Select(p=>ContactGeometry.Point(p,pose)).Aggregate(Vector3.Zero,(a,p)=>a+p)/row.Patch.Length;
                drift=Math.Max(drift,Vector3.Distance(actual,goal.Position));
                foreach(var point in row.Region)penetration=Math.Max(penetration,context.Surface.SourceFloor!.Value-Vector3.Dot(ContactGeometry.Point(point,pose),normal));
                angle=Math.Max(angle,ContactAuthoring.AngleError(PoseGeometry.Rotation(pose.World[row.Bone]),goal.Rotation));
            }
            foreach(var row in rows.Where(r=>frame>=r.Start&&frame<=r.End))
            {
                for(int i=0;i<row.Patch.Length;i++)drift=Math.Max(drift,Vector3.Distance(ContactGeometry.Point(row.Patch[i],pose),row.AnchorPoints[i]));
                foreach(var point in row.Region)penetration=Math.Max(penetration,context.Surface.SourceFloor!.Value-Vector3.Dot(ContactGeometry.Point(point,pose),normal));
                angle=Math.Max(angle,ContactAuthoring.AngleError(PoseGeometry.Rotation(pose.World[row.Bone]),row.Orientation));
            }
            for(int r=0;r<rows.Length;r++)
            {
                var row=rows[r];if(frame<=row.Start-row.Blend||frame>=row.End+row.Blend)continue;
                var geometry=full[r];skinWork+=geometry.Region.Length+geometry.Sole.Length;
                if(skinWork>MaximumKeys)throw new InvalidOperationException("Complete contact skin verification exceeds the existing two-million-work budget; no partial verification is accepted.");
                foreach(var point in geometry.Region)penetration=Math.Max(penetration,context.Surface.SourceFloor!.Value-Vector3.Dot(ContactGeometry.Point(point,pose),normal));
                if(frame>=row.Start&&frame<=row.End)for(int p=0;p<geometry.Sole.Length;p++)
                    drift=Math.Max(drift,Vector3.Distance(ContactGeometry.Point(geometry.Sole[p],pose),geometry.Anchors[p]));
            }
            if(!float.IsFinite(drift)||!float.IsFinite(penetration)||!float.IsFinite(angle)||drift>positionBound||penetration>positionBound||angle>orientationBound)
                throw new InvalidOperationException("Contact full-patch/penetration/orientation bound failed at native frame "+(frame+1)+". No result was applied.");
        }
        return(drift,penetration,angle);
    }

    private static HashSet<int> OriginalProtection(Scene scene,ReviewedContactContext context,double frame,ControlRigSetup rig)
    {
        var bones=new HashSet<int>();
        void Add(int bone){if(bone<0||bone>=scene.Bones.Length)throw new InvalidDataException("Original support references a missing native bone.");for(int i=bone;i>=0;i=scene.Bones[i].Parent)bones.Add(i);}
        foreach(var pin in context.OriginalContacts.Where(c=>c.Contains(frame)).SelectMany(c=>c.Pins))
        {
            Add(pin.Bone);
            foreach(var point in pin.SurfacePoints.Concat(pin.SurfaceRegion).Concat(pin.Clearance?.Points??[]))
                for(int i=0;i<point.InfluenceCount;i++)if(point.WeightAt(i)>0)Add((int)point.JointAt(i));
        }
        var authored=bones.ToHashSet();
        foreach(var support in context.InferredSupports.Where(s=>frame>=s.First&&frame<=s.Last))
        {
            bool replaced=context.Ranges.Any(r=>r.Reviewed&&scene.Bones[r.Bone].Name==support.BoneName&&frame>r.Start-r.Blend&&frame<r.End+r.Blend);
            if(replaced)continue;
            foreach(string name in support.Dependencies)Add(Array.FindIndex(scene.Bones,b=>b.Name==name));
        }
        if(EarlierBodyActive(context,frame))bones.ExceptWith(EditableContactBones(scene,rig,context).Except(authored));
        return bones;
    }
}
