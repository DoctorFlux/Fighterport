using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading;

namespace BrawlCrate.Studio.Retargeting
{
    public sealed class ContactGoal
    {
        public float Support { get; internal set; }
        public float Lock { get; internal set; }
        public Vector3 Anchor { get; internal set; }
        public SkinPoint Marker { get; internal set; }
    }

    // Immutable contact tracks make backwards scrubbing identical to sequential playback.
    // Surface data is format-neutral skin weights, not guessed ankle/wrist offsets.
    public sealed partial class ContactMotion
    {
        private readonly HumanoidRetargeter plan;
        private readonly Func<float,RigPose> sample;
        private readonly Dictionary<string,ContactGoal[]> tracks=new Dictionary<string,ContactGoal[]>();
        private readonly Dictionary<string,float[]> floorTracks=new Dictionary<string,float[]>();
        private readonly int last;
        private readonly HashSet<string> standingFeet=new HashSet<string>();
        public string[] StabilizedStandingFeet => standingFeet.OrderBy(r=>r).ToArray();
        public bool FloorEnabled { get; }
        public SuspendedSupport Suspended {get;}
        public float GripWeight(string side,float time)=>Suspended==null?0:Suspended.GripWeight(sample(time),side);
        public int PlantedFootSamples { get; private set; }
        public int PlantedHandSamples { get; private set; }
        public int PlantedBodySamples { get; private set; }
        public int LockedSamples { get; private set; }
        public long BodySolveIterations { get; private set; }
        public ContactMotion(HumanoidRetargeter retargeter,Func<float,RigPose> sampler,int lastFrame,bool floorEnabled,CancellationToken cancellation=default,bool requireFloorEvidence=false,bool stabilizeStanding=true,Action<int,string> progress=null,bool detectSuspended=true,bool loop=false)
        {
            plan=retargeter; sample=sampler; last=lastFrame; FloorEnabled=floorEnabled;
            if(!floorEnabled || !new[]{"left.foot","right.foot","left.hand","right.hand"}.All(r=>plan.Source.Surfaces.ContainsKey(r)&&plan.Target.Surfaces.ContainsKey(r))) return;
            RigPose[] poses=Enumerable.Range(0,last*4+1).Select(i=>{cancellation.ThrowIfCancellationRequested();if(i%4==0)progress?.Invoke(i*3/Math.Max(1,last*4),"Reading source motion...");return sample(i/4f);}).ToArray();
            Suspended=detectSuspended?SuspendedSupport.Detect(plan,poses):null;
            float[] hips=poses.Select(p=>Vector3.Dot(p.Point("pelvis"),plan.Source.GroundNormal)-plan.Source.GroundHeight).ToArray();
            float hipRange=hips.Max()-hips.Min();
            var targetPoses=new Dictionary<int,RigPose>();
            var surfaceSamples=new Dictionary<string,SurfaceSample[]>();
            int surfaceIndex=0;
            foreach(string role in plan.Source.Surfaces.Keys.Intersect(plan.Target.Surfaces.Keys))
            {
                cancellation.ThrowIfCancellationRequested();
                progress?.Invoke(3+(surfaceIndex++)*4/Math.Max(1,plan.Source.Surfaces.Count),"Analyzing floor support...");
                bool hand=role.EndsWith(".hand",StringComparison.Ordinal);
                string side=role.StartsWith("left",StringComparison.Ordinal)?"left":"right";
                float length=Length(plan.Source.Rest,side,hand);
                SurfaceSample[] surfaces=poses.Select(p=>{cancellation.ThrowIfCancellationRequested();return plan.Source.Surfaces[role].Sample(p);}).ToArray();
                surfaceSamples.Add(role,surfaces);
                if(stabilizeStanding && role.EndsWith(".foot",StringComparison.Ordinal) && hips.Min()>length*0.65f)
                {
                    float[] heights=surfaces.Select(s=>s.Clearance).OrderBy(h=>h).ToArray();
                    float range=heights[(int)((heights.Length-1)*0.95f)]-heights[(int)((heights.Length-1)*0.05f)];
                    Vector3 origin=poses[0].Point(role);
                    float travel=poses.Max(p=>{Vector3 delta=p.Point(role)-origin;delta-=plan.Source.GroundNormal*Vector3.Dot(delta,plan.Source.GroundNormal);return delta.Length();});
                    // Persistent upright support: feet move much less vertically
                    // than the hip, stay near the floor, and establish real contact.
                    // This distinguishes small source FK sole drift from a step,
                    // jump, held-up foot, or a whole-body airborne trajectory.
                    if(heights.Any(h=>h>=-length*0.25f && h<=length*0.015f) &&
                        heights[(int)((heights.Length-1)*0.05f)]>=-length*0.03f &&
                        heights[(int)((heights.Length-1)*0.95f)]<length*0.12f &&
                        range<Math.Max(length*0.035f,hipRange*0.65f) && travel<length*0.5f)standingFeet.Add(role);
                }
                if(stabilizeStanding && role.EndsWith(".foot",StringComparison.Ordinal))
                {
                    float[] stance=standingFeet.Contains(role)?Enumerable.Repeat(1f,poses.Length).ToArray():LocalStance(poses,surfaces,hips,role,length);
                    stanceTracks.Add(role,stance);
                    if(stance.Any(w=>w>0.99f))standingFeet.Add(role);
                }
                float[] support=new float[poses.Length], stationary=new float[poses.Length],floor=new float[poses.Length];
                bool belowPlane=false;int settled=0;
                for(int i=0;i<poses.Length;i++)
                {
                    int before=Math.Max(0,i-1),after=Math.Min(poses.Length-1,i+1);
                    float dt=Math.Max(0.25f,(after-before)/4f);
                    // A stationary wrist/ankle does not imply a stationary skin
                    // contact when fingers, toes or the contact patch are rolling.
                    Vector3 velocity=(surfaces[after].Point-surfaces[before].Point)/dt;
                    float vertical=Math.Abs(Vector3.Dot(velocity,plan.Source.GroundNormal));
                    Vector3 planar=velocity-plan.Source.GroundNormal*Vector3.Dot(velocity,plan.Source.GroundNormal);
                    float h=surfaces[i].Clearance;
                    // Modest authored penetration is contact; deep off-plane motion is not.
                    bool body=!IsEndContact(role);
                    // Emerging through a reference plane is different from
                    // landing on it. Keep each surface off the floor solver
                    // until it clears the contact band or genuinely settles.
                    if(h<-length*0.25f){belowPlane=true;settled=0;}
                    float heightSpeed=Math.Abs(surfaces[after].Clearance-surfaces[before].Clearance)/dt;
                    settled=h>=-length*0.1f && heightSpeed<length*0.01f?settled+1:0;
                    if(belowPlane && (h>length*(body?0.02f:0.055f) || settled>=4 ||
                        (stanceTracks.TryGetValue(role,out float[] standing) &&
                            (standing[i]>0.99f || (standing[i]>0 && h>=0)))))belowPlane=false;
                    floor[i]=belowPlane?0:1;
                    float penetration=Smooth(-length*0.25f,-length*0.1f,h);
                    support[i]=penetration*(1-Smooth(length*(body?0.003f:0.015f),length*(body?0.02f:0.055f),Math.Max(0,h)));
                    support[i]*=1-Smooth(length*0.1f,length*0.3f,vertical);
                    if(stanceTracks.TryGetValue(role,out float[] stance) && stance[i]>0)
                    {
                        // Confirmed posture already distinguishes support from
                        // leg lifting. FK skin drift during hip bounce or
                        // travel must not undo that inference. Surface velocity
                        // still independently determines stationary locking.
                        float persistent=1-Smooth(length*0.12f,length*0.18f,Math.Max(0,h));
                        support[i]=Math.Max(support[i],persistent*penetration*stance[i]);
                    }
                    stationary[i]=1-Smooth(length*0.015f,length*0.05f,planar.Length());
                    if(Suspended!=null)support[i]*=Suspended.FloorWeight(poses[i],role);
                    support[i]*=floor[i];
                }
                var floorWeights=(float[])floor.Clone();
                for(int i=0;i<floor.Length;i++)for(int d=1;d<=3;d++)
                    floorWeights[i]=Math.Min(floorWeights[i],Math.Min(floor[Math.Max(0,i-d)]+d/4f,floor[Math.Min(floor.Length-1,i+d)]+d/4f));
                floorTracks.Add(role,floorWeights);
                var values=new ContactGoal[poses.Length];
                for(int i=0;i<values.Length;i++)
                {
                    float weight=support[i];
                    for(int d=1;d<=3;d++) weight=Math.Min(weight,Math.Min(support[Math.Max(0,i-d)]+d/4f,support[Math.Min(values.Length-1,i+d)]+d/4f));
                    values[i]=new ContactGoal{Support=weight,Lock=weight*stationary[i]};
                    if(weight>0.99f) { if(hand) PlantedHandSamples++; else if(role.EndsWith(".foot",StringComparison.Ordinal))PlantedFootSamples++;else PlantedBodySamples++; }
                }
                int start=0;
                while(start<values.Length)
                {
                    if(values[start].Lock<0.8f){values[start].Lock=0;start++;continue;}
                    int end=start; while(end+1<values.Length&&values[end+1].Lock>=0.8f)end++;
                    if(end-start<3){for(int i=start;i<=end;i++)values[i].Lock=0;start=end+1;continue;}
                    if(!targetPoses.TryGetValue(start,out RigPose target)) {var posed=plan.Evaluate(poses[start]);Suspended?.Apply(poses[start],posed);target=posed.Pose;targetPoses.Add(start,target);}
                    SkinPoint marker=plan.Target.Surfaces[role].Marker(target);
                    Vector3 anchor=marker.Evaluate(target);
                    anchor+=plan.Target.GroundNormal*(plan.Target.GroundHeight-Vector3.Dot(anchor,plan.Target.GroundNormal));
                    for(int i=start;i<=end;i++)
                    {
                        values[i].Anchor=anchor;
                        values[i].Marker=marker;
                        values[i].Lock*=Smooth(0,1,Math.Min(1,Math.Min((i-start+1)/4f,(end-i+1)/4f)));
                        if(values[i].Lock>0.99f)LockedSamples++;
                    }
                    start=end+1;
                }
                tracks.Add(role,values);
            }
            if(requireFloorEvidence)InferGroundEvidence(poses,surfaceSamples,loop);
            if(requireFloorEvidence && Suspended==null && !tracks.Values.Any(values=>HasSustainedSupport(values,Math.Min(3,values.Length))))
            {
                FloorEnabled=false;tracks.Clear();PlantedFootSamples=PlantedHandSamples=PlantedBodySamples=LockedSamples=0;return;
            }
            CalibrateBodyAnchors(cancellation,(done,total)=>progress?.Invoke(7+done*8/Math.Max(1,total),"Calibrating ground contacts ("+done+"/"+total+")..."));
        }
        private static bool HasSustainedSupport(ContactGoal[] values,int minimum)
        {
            int run=0;foreach(var value in values){run=value.Support>0.5f?run+1:0;if(run>=minimum)return true;}return false;
        }
        private static float Smooth(float a,float b,float v) {float t=RigMath.Clamp((v-a)/(b-a),0,1);return t*t*(3-2*t);}
        // Near-complete support must converge to the plane, even if the initial
        // FK offset is large on a differently proportioned rig. Keep a smooth
        // transition and use the same 0.99 full-contact boundary as diagnostics.
        private static float HeightRemainder(float support)
        {float weight=RigMath.Clamp(support,0,1);return (1-weight)*(1-Smooth(0.9f,0.99f,weight));}
        private static float Length(RigPose pose,string side,bool hand)
        {return Vector3.Distance(pose.Point(side+(hand?".upperArm":".upperLeg")),pose.Point(side+(hand?".lowerArm":".lowerLeg")))+
            Vector3.Distance(pose.Point(side+(hand?".lowerArm":".lowerLeg")),pose.Point(side+(hand?".hand":".foot")));}
        internal Dictionary<string,Tuple<float,float>> NearFloorFeet(float time)
            =>NearFloorFeet(sample(time),tracks.Keys.ToDictionary(r=>r,r=>At(r,time)),time);
        private Dictionary<string,Tuple<float,float>> NearFloorFeet(RigPose source,Dictionary<string,ContactGoal> controls,float time)
        {
            var goals=new Dictionary<string,Tuple<float,float>>();
            foreach(string side in new[]{"left","right"})
            {
                string role=side+".foot";
                float height=plan.Source.Surfaces[role].Clearance(source),length=Length(plan.Source.Rest,side,false);
                float near=1-Smooth(length*0.1f,length*0.3f,Math.Max(0,height));
                near*=GroundEvidenceAt(time);
                // A source clip may cross far below its static reference floor
                // (for example when gameplay supplies the jump trajectory).
                // Fade the constraint rather than snapping the limb back to FK
                // at an arbitrary penetration threshold between native keys.
                near*=Smooth(-length*0.25f,-length*0.1f,height);
                if(floorTracks.TryGetValue(role,out float[] floor))
                {
                    float index=RigMath.Clamp(time*4,0,floor.Length-1);int a=(int)index,b=Math.Min(floor.Length-1,a+1);
                    // Once a lifted foot emerges, fit its source-scaled height
                    // even while it is still moving. This is not a planted lock.
                    near*=Math.Max(floor[a]+(floor[b]-floor[a])*(index-a),Smooth(-length*0.05f,0,height));
                }
                if(Suspended!=null)near*=Suspended.FloorWeight(source,role);
                float desired=Math.Max(0,height)*Length(plan.Target.Rest,side,false)/length*HeightRemainder(controls[role].Support);
                goals.Add(role,Tuple.Create(near,desired));
            }
            return goals;
        }
        public ContactGoal At(string role,float time)
        {
            if(!tracks.TryGetValue(role,out ContactGoal[] values))return new ContactGoal();
            float index=RigMath.Clamp(time*4,0,last*4); int a=(int)index,b=Math.Min(values.Length-1,a+1);float t=index-a;
            return new ContactGoal {Support=values[a].Support*(1-t)+values[b].Support*t, Lock=values[a].Lock*(1-t)+values[b].Lock*t,
                Anchor=values[a].Lock>0?values[a].Anchor:values[b].Anchor,Marker=values[a].Marker??values[b].Marker};
        }
        public RetargetPose Evaluate(float time)
        {
            RigPose source=sample(time);
            if(TryCachedPose(time,source,out RetargetPose cached))return cached;
            RetargetPose result=plan.Evaluate(source);
            Vector3 uncorrectedPelvis=result.Pose.Point("pelvis");
            Suspended?.Apply(source,result);
            if(tracks.Count==0)return result;
            RigPose target=result.Pose; Vector3 up=plan.Target.GroundNormal;
            Vector3 originalPelvis=uncorrectedPelvis;
            var bodyGoals=new Dictionary<string,Vector3>(result.Goals);
            var localGoals=new Dictionary<string,Vector3>();
            var goalParents=new Dictionary<string,int>();
            foreach(var goal in bodyGoals)
            {
                string side=goal.Key.Split('.')[0];bool hand=goal.Key.EndsWith(".hand",StringComparison.Ordinal);
                int parent=target.Rig.Joints[target.Rig.Roles[side+(hand?".upperArm":".upperLeg")]].Parent;
                Matrix4x4.Invert(target.World[parent],out Matrix4x4 inverse);
                localGoals.Add(goal.Key,Vector3.Transform(goal.Value,inverse));goalParents.Add(goal.Key,parent);
            }
            var controls=tracks.Keys.ToDictionary(role=>role,role=>At(role,time));
            var nearFeet=NearFloorFeet(source,controls,time);
            if(controls.Values.All(c=>c.Support<=0) && nearFeet.Values.All(v=>v.Item1<=0))return result;
            var bendDirections=new Dictionary<string,Vector3>();
            foreach(string role in tracks.Keys.Where(IsEndContact))
            {
                bool hand=role.EndsWith(".hand",StringComparison.Ordinal); string side=role.StartsWith("left",StringComparison.Ordinal)?"left":"right";
                string root=side+(hand?".upperArm":".upperLeg"),middle=side+(hand?".lowerArm":".lowerLeg");
                Vector3 axis=RigMath.Unit(source.Point(role)-source.Point(root));
                Vector3 bend=source.Point(middle)-source.Point(root); bend-=axis*Vector3.Dot(bend,axis);
                if(bend.LengthSquared()<0.00001f)
                {
                    Quaternion motion=source.Orientation[source.Rig.Roles[root]]*Quaternion.Inverse(source.Rig.Rest.Orientation[source.Rig.Roles[root]]);
                    bend=Vector3.Transform(Vector3.Transform(Vector3.UnitZ,RigMath.BodyFrame(source.Rig)),motion);
                }
                bendDirections[role]=Vector3.Transform(bend,plan.Calibration);
            }
            // Flatten only genuinely flat source contacts; preserve heel/toe rolls and FK.
            foreach(var pair in controls.Where(p=>p.Value.Support>0))
            {
                float flatness=plan.Source.Surfaces[pair.Key].Sample(source).Flatness;
                if(flatness<=0)continue;
                int joint=target.Rig.Roles[pair.Key];
                Vector3 normal=Vector3.Transform(target.Rig.Surfaces[pair.Key].NormalLocal,target.Orientation[joint]);
                if(pair.Key.EndsWith(".hand",StringComparison.Ordinal) && Vector3.Dot(normal,up)<0)normal=-normal;
                Quaternion aligned=RigMath.FromTo(normal,up)*target.Orientation[joint];
                target.SetWorldRotation(joint,Quaternion.Slerp(target.Orientation[joint],aligned,pair.Value.Support*flatness));
            }
            FitStancePelvis(source,target,controls,time);
            // An arm bearing weight far below the initial FK pose needs torso
            // articulation too. Solve it before reach projection can turn the
            // entire correction into a large, abrupt pelvis drop.
            if(!controls.Any(c=>!IsEndContact(c.Key) && c.Value.Support>0.001f) &&
                controls.Any(c=>c.Key.EndsWith(".hand",StringComparison.Ordinal) && c.Value.Support>0.1f &&
                    plan.Target.Surfaces[c.Key].Clearance(target)>Length(plan.Target.Rest,c.Key.Split('.')[0],true)*0.3f))
                FitBodyContacts(target,controls,nearFeet,true);
            for(int iteration=0;iteration<5;iteration++)
            {
                Vector3 bodyShift=target.Point("pelvis")-originalPelvis;
                var goals=localGoals.ToDictionary(p=>p.Key,p=>Vector3.Transform(p.Value,target.World[goalParents[p.Key]]));
                foreach(var pair in controls.Where(p=>IsEndContact(p.Key) && (p.Value.Support>0 || (nearFeet.ContainsKey(p.Key)&&nearFeet[p.Key].Item1>0))))
                {
                    string role=pair.Key; ContactGoal control=pair.Value;
                    SurfaceSample surface=plan.Target.Surfaces[role].Sample(target);
                    // Five passes must share the transition weight; applying the
                    // full weight on every pass makes a soft landing nearly snap.
                    float heightWeight=control.Support,desiredHeight=0;
                    if(nearFeet.TryGetValue(role,out Tuple<float,float> near)){heightWeight=Math.Max(heightWeight,near.Item1);desiredHeight=near.Item2;}
                    float blend=1-(float)Math.Pow(1-heightWeight,0.2);
                    Vector3 delta=up*(desiredHeight-surface.Clearance)*blend;
                    Vector3 planar=control.Anchor-(control.Marker==null?surface.Point:control.Marker.Evaluate(target)); planar-=up*Vector3.Dot(planar,up);
                    delta+=planar*(1-(float)Math.Pow(1-control.Lock,0.2));
                    goals[role]=target.Point(role)+delta;
                }
                // Regain reach by crouching the pelvis, preserving segment lengths and
                // authored root tracks instead of clamping hands/feet above the floor.
                Vector3 correction=Vector3.Zero;
                for(int projection=0;projection<8;projection++)
                foreach(var pair in controls.Where(p=>IsEndContact(p.Key) && (p.Value.Support>0.001f || (nearFeet.ContainsKey(p.Key)&&nearFeet[p.Key].Item1>0.001f))))
                {
                    bool hand=pair.Key.EndsWith(".hand",StringComparison.Ordinal); string side=pair.Key.StartsWith("left",StringComparison.Ordinal)?"left":"right";
                    Vector3 start=target.Point(side+(hand?".upperArm":".upperLeg")), delta=start+correction-goals[pair.Key];
                    float distance=delta.Length(),reach=Length(target,side,hand)*0.9998f;
                    float strength=pair.Value.Support;
                    if(nearFeet.TryGetValue(pair.Key,out Tuple<float,float> near))strength=Math.Max(strength,near.Item1);
                    if(distance>reach)correction-=delta*((distance-reach)/distance)*strength;
                }
                float maximum=Length(plan.Target.Rest,"left",false)*0.8f;
                if(correction.Length()>maximum)correction=Vector3.Normalize(correction)*maximum;
                if(correction.LengthSquared()>0.0000000001f)
                {
                    target.SetWorldPoint(target.Rig.Roles["pelvis"],target.Point("pelvis")+correction);
                    foreach(string role in goals.Keys.ToArray())
                    {
                        Vector3 shift=correction*(1-controls[role].Support);
                        if(nearFeet.TryGetValue(role,out Tuple<float,float> near))shift-=up*Vector3.Dot(correction,up)*Math.Max(0,near.Item1-controls[role].Support);
                        goals[role]+=shift;
                    }
                }
                foreach(var goal in goals)
                {
                    bool hand=goal.Key.EndsWith(".hand",StringComparison.Ordinal); string side=goal.Key.StartsWith("left",StringComparison.Ordinal)?"left":"right";
                    string middle=side+(hand?".lowerArm":".lowerLeg"),root=side+(hand?".upperArm":".upperLeg");
                    Vector3 bend=bendDirections[goal.Key];
                    if(controls.TryGetValue(middle,out ContactGoal bodyContact) && bodyContact.Support>0)
                        bend=Vector3.Lerp(bend,target.Point(middle)-target.Point(root),bodyContact.Support);
                    plan.SolveLimb(target,result,side,hand,goal.Value,bend); result.Goals[goal.Key]=goal.Value;
                }
            }
            bool bodyActive=controls.Any(c=>!IsEndContact(c.Key) && c.Value.Support>0.001f);
            // A near-complete end contact can retain a visible fraction of a
            // large FK gap despite the soft analytic passes. Escalate measured
            // residuals to the coupled solver even without body skin support.
            bool unresolvedEnd=controls.Any(c=>IsEndContact(c.Key) && c.Value.Support>0.99f &&
                Math.Abs(plan.Target.Surfaces[c.Key].Clearance(target))>Length(plan.Target.Rest,c.Key.Split('.')[0],c.Key.EndsWith(".hand",StringComparison.Ordinal))*0.001f);
            FitBodyContacts(target,controls,nearFeet,unresolvedEnd);
            result.PelvisCorrection=target.Point("pelvis")-originalPelvis;
            foreach(var pair in controls.Where(p=>p.Value.Support>0.99f))result.MaximumContactResidual=Math.Max(result.MaximumContactResidual,Math.Abs(plan.Target.Surfaces[pair.Key].Sample(target).Clearance));
            if(bodyActive)CachePose(time,source,result);
            return result;
        }
        private void FitStancePelvis(RigPose source,RigPose target,Dictionary<string,ContactGoal> controls,float time)
        {
            // Transfer the source's supported leg height, including hip bounce,
            // rather than scaling hip translation about an unrelated target rest
            // height. Seated/lying/hand-supported poses use the body solve instead.
            if(standingFeet.Count==0 || controls.Any(p=>!p.Key.EndsWith(".foot",StringComparison.Ordinal)&&p.Value.Support>0.001f))return;
            Vector3 up=target.Rig.GroundNormal;float desired=0,total=0,maximum=0;
            foreach(string side in new[]{"left","right"})
            {
                string role=side+".foot";float support=controls[role].Support*StanceAt(role,time);
                if(support<=0.001f)continue;
                float ratio=Length(target.Rig.Rest,side,false)/Length(source.Rig.Rest,side,false);
                float sourceLegHeight=Vector3.Dot(source.Point(side+".upperLeg")-source.Point(role),source.Rig.GroundNormal)*ratio;
                float ankleHeight=Vector3.Dot(target.Point(role),up)-target.Rig.Surfaces[role].Clearance(target);
                float rootOffset=Vector3.Dot(target.Point(side+".upperLeg")-target.Point("pelvis"),up);
                float soleHeight=Math.Max(0,source.Rig.Surfaces[role].Clearance(source))*ratio*(1-support);
                float weight=support*support;desired+=(ankleHeight+soleHeight+sourceLegHeight-rootOffset)*weight;total+=weight;maximum=Math.Max(maximum,support);
            }
            if(total>0)
            {
                Vector3 pelvis=target.Point("pelvis");
                target.SetWorldPoint(target.Rig.Roles["pelvis"],pelvis+up*((desired/total-Vector3.Dot(pelvis,up))*maximum));
            }
        }
    }
}
