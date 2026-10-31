using BrawlLib.SSBB.ResourceNodes;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading;

namespace BrawlCrate.Studio.Retargeting
{
    public static partial class BrawlRetargeter
    {
        // Integer keys cannot represent the quaternion solver exactly between keys.
        // Refine their shared native tangents against skinned contact heights, with
        // a pose penalty and bounded displacement. Keys, duration and root stay intact.
        private static void FitContactCurves(BrawlRig target,CHR0Node output,ContactMotion contacts,int last,CancellationToken cancellation,Action<int,string> progress,ISet<int> mappedTargets)
        {
            if(!contacts.FloorEnabled || last==0)return;
            var sampler=new DenseCurveSampler(target,output,last);
            var reference=new Dictionary<float,RigPose>();
            var solvedPoses=new Dictionary<float,RigPose>();
            var poseErrors=new Dictionary<float,float>();
            var heights=new Dictionary<float,Dictionary<string,Tuple<float,float>>>();
            var contactTracks=new Dictionary<float,Dictionary<string,ContactGoal>>();
            var gripWeights=new Dictionary<float,float[]>();
            var eligible=new HashSet<int>();
            float poseTolerance=Vector3.Distance(target.Rig.Rest.Point("left.upperLeg"),target.Rig.Rest.Point("left.foot"))*0.1f;
            for(int i=1;i<last*4;i++)
            {
                cancellation.ThrowIfCancellationRequested();
                if(i%4==0)continue;
                float time=i/4f;var pose=sampler.Sample(time);
                reference.Add(time,pose);
                RigPose solved=contacts.Evaluate(time).Pose;
                var nearFeet=contacts.NearFloorFeet(time);
                contactTracks.Add(time,target.Rig.Surfaces.Keys.ToDictionary(r=>r,r=>contacts.At(r,time)));
                solvedPoses.Add(time,solved);
                gripWeights.Add(time,new[]{contacts.GripWeight("left",time),contacts.GripWeight("right",time)});
                poseErrors.Add(time,Enumerable.Range(0,target.Bones.Length).Max(b=>Vector3.Distance(pose.Point(b),solved.Point(b))));
                heights.Add(time,target.Rig.Surfaces.ToDictionary(p=>p.Key,p=>{
                    float support=contactTracks[time][p.Key].Support;
                    return Tuple.Create(nearFeet.ContainsKey(p.Key)?Math.Max(support,nearFeet[p.Key].Item1):support,p.Value.Clearance(solved));
                }));
                bool bodySupported=contactTracks[time].Any(c=>!ContactMotion.IsEndContact(c.Key) && c.Value.Support>0.001f);
                if((!bodySupported && poseErrors[time]>poseTolerance) || new[]{"left","right"}.Where((side,sideIndex)=>gripWeights[time][sideIndex]>0.001f).Any(side=>Vector3.Distance(pose.Point(side+".hand"),solved.Point(side+".hand"))>0.02f) || target.Rig.Surfaces.Any(p=>{
                    if(heights[time][p.Key].Item1>0.001f && Math.Abs(p.Value.Clearance(pose)-heights[time][p.Key].Item2)>0.02f)return true;
                    var contact=contactTracks[time][p.Key];
                    if(contact.Lock<0.99f || contact.Marker==null)return false;
                    Vector3 delta=contact.Marker.Evaluate(pose)-contact.Anchor;
                    delta-=target.Rig.GroundNormal*Vector3.Dot(delta,target.Rig.GroundNormal);
                    return delta.Length()>0.02f;
                }))
                {eligible.Add(i/4);eligible.Add(i/4+1);}
            }
            var poseWeights=reference.Keys.ToDictionary(t=>t,t=>{
                bool bodySupported=contactTracks[t].Any(c=>!ContactMotion.IsEndContact(c.Key) && c.Value.Support>0.001f);
                int[] freeBranches=new[]{"left","right"}.SelectMany(side=>new[]{"hand","foot"}.Where(end=>target.Rig.Roles.ContainsKey(side+"."+end)&&contactTracks[t][side+"."+end].Support<=0.001f)
                    .Select(end=>target.Rig.Roles[side+(end=="hand"?".upperArm":".upperLeg")])).ToArray();
                return Enumerable.Range(0,target.Bones.Length).Select(b=>!bodySupported && poseErrors[t]>poseTolerance && freeBranches.Any(j=>target.Rig.Ancestors[b][j])?0.5f:0.025f).ToArray();
            });
            int completed=0;
            const int maximumPasses=40;
            for(int pass=0;pass<maximumPasses;pass++)
            {
            bool passImproved=false;
            foreach(int key in eligible.OrderBy(i=>i))
            {
                cancellation.ThrowIfCancellationRequested();
                progress?.Invoke(65+(completed++)*35/Math.Max(1,eligible.Count*maximumPasses),"Preserving native ground contacts...");
                // Shared tangents couple neighboring intervals. Repair the
                // remaining difficult contacts with neighboring keys together instead
                // of oscillating between independent one-key improvements.
                int keyCount=Math.Min(pass<24?1:pass<32?2:3,last-key+1);
                float[] times=Enumerable.Range(-3,keyCount*4+3).Where(i=>i%4!=0).Select(i=>key+i/4f).Where(t=>t>0 && t<last).ToArray();
                var roles=target.Rig.Surfaces.Keys.Where(r=>times.Any(t=>heights[t][r].Item1>0.001f))
                    .Concat(new[]{"left","right"}.Where((side,i)=>times.Any(t=>gripWeights[t][i]>0.001f)).Select(side=>side+".hand")).Distinct().ToArray();
                if(times.All(t=>{
                    RigPose pose=sampler.Sample(t);
                    float precision=poseTolerance*(pass<24?0.05f:0.1f);
                    if(new[]{"left","right"}.Where((side,i)=>gripWeights[t][i]>0.001f).Any(side=>Vector3.Distance(pose.Point(side+".hand"),solvedPoses[t].Point(side+".hand"))>0.025f))return false;
                    if(!contactTracks[t].Any(c=>!ContactMotion.IsEndContact(c.Key) && c.Value.Support>0.001f) &&
                        Enumerable.Range(0,target.Bones.Length).Any(b=>Vector3.Distance(pose.Point(b),solvedPoses[t].Point(b))>poseTolerance))return false;
                    return roles.All(r=>{
                        if(Math.Abs(target.Rig.Surfaces[r].Clearance(pose)-heights[t][r].Item2)*heights[t][r].Item1>=precision)return false;
                        var contact=contactTracks[t][r];if(contact.Lock<0.99f || contact.Marker==null)return true;
                        Vector3 delta=contact.Marker.Evaluate(pose)-contact.Anchor;
                        delta-=target.Rig.GroundNormal*Vector3.Dot(delta,target.Rig.GroundNormal);
                        return delta.Length()<precision;
                    });
                }))continue;
                float budget=Math.Max(Vector3.Distance(target.Rig.Rest.Point("left.upperLeg"),target.Rig.Rest.Point("left.foot"))*0.015f,
                    times.Max(t=>roles.Where(r=>heights[t][r].Item1>0.001f).Select(r=>Math.Abs(target.Rig.Surfaces[r].Clearance(reference[t])-heights[t][r].Item2)*1.5f).DefaultIfEmpty(0).Max()));
                budget=Math.Max(budget,times.Max(t=>poseErrors[t])*1.25f);
                var variables=new List<Tuple<int,int>>();
                int pelvis=target.Rig.Roles["pelvis"];
                for(int channel=6;channel<9;channel++)variables.Add(Tuple.Create(pelvis,channel));
                foreach(string role in roles)
                {
                    foreach(int joint in ContactMotion.JointControls(target.Rig,role))
                        for(int channel=3;channel<6;channel++)variables.Add(Tuple.Create(joint,channel));
                }
                // Unsupported limbs can also deviate visibly between integer
                // keys. Include their anatomical ancestors when the initial
                // native approximation exceeds a proportion-scaled threshold.
                var inaccurate=times.Where(t=>!contactTracks[t].Any(c=>!ContactMotion.IsEndContact(c.Key) && c.Value.Support>0.001f)).SelectMany(t=>{
                    RigPose current=sampler.Sample(t);
                    return Enumerable.Range(0,target.Bones.Length).Where(b=>Vector3.Distance(current.Point(b),solvedPoses[t].Point(b))>poseTolerance);
                }).Distinct().ToArray();
                foreach(var role in target.Rig.Roles.Where(r=>target.Rig.Ancestors[r.Value][pelvis] && !r.Key.Contains(".finger") && !r.Key.EndsWith(".toe",StringComparison.Ordinal)))
                    if(inaccurate.Any(b=>target.Rig.Ancestors[b][role.Value]))
                        for(int channel=3;channel<6;channel++)variables.Add(Tuple.Create(role.Value,channel));
                if(roles.Any(r=>!r.EndsWith(".hand",StringComparison.Ordinal)&&!r.EndsWith(".foot",StringComparison.Ordinal)))
                    foreach(string role in new[]{"pelvis","spine","chest","neck"})for(int channel=3;channel<6;channel++)variables.Add(Tuple.Create(target.Rig.Roles[role],channel));
                // Optional target-only helpers must retain native local rest;
                // contact fitting cannot create a new source correspondence.
                var baseControls=variables.Where(v=>mappedTargets.Contains(v.Item1)).Distinct().ToArray();
                var controls=Enumerable.Range(key,keyCount).SelectMany(k=>baseControls.Select(v=>Tuple.Create(v.Item1,v.Item2,k))).ToArray();
                var tangents=controls.Select(v=>sampler.Keys[v.Item1][v.Item2][v.Item3]).ToArray();
                float[] original=tangents.Select(t=>t._tangent).ToArray();
                var curveReference=times.ToDictionary(t=>t,t=>sampler.Sample(t));
                int[] changedBones=controls.Select(c=>c.Item1).Distinct().ToArray();
                double[] residual=ContactResiduals(target,sampler,contactTracks,times,reference,solvedPoses,heights,gripWeights,out _,curveReference:curveReference,poseWeights:poseWeights);
                double loss=residual.Sum(v=>v*v);
                if(loss<1e-8)continue;
                // The derivative of a minimum uses its currently active skin
                // vertex. Freeze that active set for the finite differences;
                // line-search acceptance still evaluates the complete skin.
                var derivativePoints=times.ToDictionary(t=>t,t=>{
                    RigPose pose=curveReference[t];
                    return target.Rig.Surfaces.ToDictionary(s=>s.Key,s=>{
                        if(heights[t][s.Key].Item1<=0.001f)return null;
                        return s.Value.LowestPoint(pose,out _);
                    });
                });
                var columns=new double[controls.Length][];
                for(int variable=0;variable<controls.Length;variable++)
                {
                    float epsilon=controls[variable].Item2<6?0.1f:0.01f;
                    tangents[variable]._tangent=original[variable]+epsilon;
                    double[] perturbed=ContactResiduals(target,sampler,contactTracks,times,reference,solvedPoses,heights,gripWeights,out _,derivativePoints,curveReference,new[]{controls[variable].Item1},poseWeights);
                    tangents[variable]._tangent=original[variable];
                    columns[variable]=perturbed.Select((v,i)=>(v-residual[i])/epsilon).ToArray();
                }
                var normal=new double[controls.Length,controls.Length];var rhs=new double[controls.Length];
                for(int a=0;a<controls.Length;a++)
                {
                    for(int row=0;row<residual.Length;row++)rhs[a]-=columns[a][row]*residual[row];
                    for(int b=0;b<=a;b++)
                    {
                        double value=0;for(int row=0;row<residual.Length;row++)value+=columns[a][row]*columns[b][row];
                        normal[a,b]=normal[b,a]=value;
                    }
                    normal[a,a]+=0.000001;
                }
                var descent=Enumerable.Range(0,controls.Length).Select(i=>rhs[i]/normal[i,i]).ToArray();
                var translationNormal=new double[3,3];var translationRhs=new double[3];
                for(int a=0;a<3;a++){translationRhs[a]=rhs[a];for(int b=0;b<3;b++)translationNormal[a,b]=normal[a,b];}
                double[] translation=new double[controls.Length];
                Array.Copy(ContactMotion.SolveContactSystem(translationNormal,translationRhs),translation,3);
                double[] step=ContactMotion.SolveContactSystem(normal,rhs);bool improved=false;
                float[] best=(float[])original.Clone();double bestLoss=loss;
                for(int direction=0;direction<3;direction++)
                {
                    if(direction==1)step=descent;
                    if(direction==2)step=translation;
                    for(int variable=0;variable<controls.Length;variable++)
                    {
                        double limit=controls[variable].Item2<6?45:0.5;
                        step[variable]=Math.Max(-limit,Math.Min(limit,step[variable]));
                    }
                    float factor=1;
                    for(int attempt=0;attempt<10;attempt++)
                    {
                        for(int variable=0;variable<controls.Length;variable++)tangents[variable]._tangent=original[variable]+(float)step[variable]*factor;
                        double[] candidate=ContactResiduals(target,sampler,contactTracks,times,reference,solvedPoses,heights,gripWeights,out float displacement,curveReference:curveReference,changedBones:changedBones,poseWeights:poseWeights);
                        double candidateLoss=candidate.Sum(v=>v*v);
                        if(displacement<budget && candidateLoss<bestLoss)
                        {
                            improved=true;bestLoss=candidateLoss;
                            for(int variable=0;variable<controls.Length;variable++)best[variable]=tangents[variable]._tangent;
                            break;
                        }
                        factor*=0.5f;
                    }
                }
                for(int variable=0;variable<controls.Length;variable++)tangents[variable]._tangent=best[variable];
                if(improved)passImproved=true;
            }
            if(!passImproved)
            {
                // A stalled single-key solve does not establish convergence
                // for coupled intervals. Always try the next neighborhood.
                if(pass<24)pass=23;
                else if(pass<32)pass=31;
                else break;
            }
            }
        }
        private static double[] ContactResiduals(BrawlRig target,DenseCurveSampler sampler,Dictionary<float,Dictionary<string,ContactGoal>> contactTracks,float[] times,
            Dictionary<float,RigPose> reference,Dictionary<float,RigPose> solvedPoses,Dictionary<float,Dictionary<string,Tuple<float,float>>> heights,Dictionary<float,float[]> gripWeights,out float displacement,
            Dictionary<float,Dictionary<string,SkinPoint>> derivativePoints=null,Dictionary<float,RigPose> curveReference=null,int[] changedBones=null,Dictionary<float,float[]> poseWeights=null)
        {
            var residuals=new List<double>();displacement=0;
            float contactScale=Math.Max(0.00001f,Vector3.Distance(target.Rig.Rest.Point("left.upperLeg"),target.Rig.Rest.Point("left.foot"))*0.02f);
            foreach(float time in times)
            {
                RigPose pose=curveReference==null?sampler.Sample(time):changedBones==null?curveReference[time]:sampler.Sample(time,curveReference[time],changedBones),original=reference[time];
                foreach(var surface in target.Rig.Surfaces)
                {
                    ContactGoal control=contactTracks[time][surface.Key];
                    var goal=heights[time][surface.Key];
                    float error=goal.Item1>0.001f?(derivativePoints==null?surface.Value.Clearance(pose):
                        Vector3.Dot(derivativePoints[time][surface.Key].Evaluate(pose),target.Rig.GroundNormal)-target.Rig.GroundHeight)-goal.Item2:0;
                    bool end=ContactMotion.IsEndContact(surface.Key);
                    float severity=RigMath.Clamp((Math.Abs(error)*goal.Item1-contactScale)/(contactScale*2),0,1);
                    severity=1+4*severity*severity*(3-2*severity);
                    residuals.Add(error*goal.Item1*(end?20:10)*severity);
                    Vector3 planar=Vector3.Zero;
                    if(control.Lock>0.99f && control.Marker!=null)
                    {
                        planar=control.Marker.Evaluate(pose)-control.Anchor;
                        planar-=target.Rig.GroundNormal*Vector3.Dot(planar,target.Rig.GroundNormal);
                    }
                    // Sliding needs the same proportion-scaled escalation as
                    // floor penetration. A fixed planar weight can sacrifice a
                    // planted sole to a competing fractional height residual.
                    float slide=RigMath.Clamp((planar.Length()-contactScale)/(contactScale*2),0,1);
                    float lockWeight=10*(1+4*slide*slide*(3-2*slide));
                    residuals.Add(planar.X*lockWeight);residuals.Add(planar.Y*lockWeight);residuals.Add(planar.Z*lockWeight);
                }
                for(int side=0;side<2;side++)
                {
                    string role=(side==0?"left":"right")+".hand";
                    Vector3 delta=(pose.Point(role)-solvedPoses[time].Point(role))*gripWeights[time][side]*40;
                    residuals.Add(delta.X);residuals.Add(delta.Y);residuals.Add(delta.Z);
                }
                for(int i=0;i<target.Bones.Length;i++)
                {
                    Vector3 delta=pose.Point(i)-original.Point(i);
                    displacement=Math.Max(displacement,delta.Length());
                    Vector3 poseError=pose.Point(i)-solvedPoses[time].Point(i);
                    float poseWeight=poseWeights==null?0.025f:poseWeights[time][i];
                    residuals.Add(poseError.X*poseWeight);residuals.Add(poseError.Y*poseWeight);residuals.Add(poseError.Z*poseWeight);
                }
            }
            return residuals.ToArray();
        }
    }
}
