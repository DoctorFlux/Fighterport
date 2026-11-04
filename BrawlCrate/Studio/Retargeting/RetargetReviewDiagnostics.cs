using BrawlLib.SSBB.ResourceNodes;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Threading;

namespace BrawlCrate.Studio.Retargeting
{
    // Prepared-result review only. The model's bind-sole guide is not a
    // gameplay floor, and source-derived contact weights are proposals.
    internal static class RetargetReviewDiagnostics
    {
        private const long MaximumSkinSamples = 2000000;

        internal static string Analyze(BrawlRig source,BrawlRig target,CHR0Node sourceClip,
            RetargetResult result,CancellationToken cancellation)
        {
            cancellation.ThrowIfCancellationRequested();
            // An exact same-rig native copy introduces no new morphology or
            // inferred floor contact to review.
            if(result.OptimizationReport?.StartsWith("Exact native rig:",StringComparison.Ordinal)==true &&
                (result.Contacts?.PlantedHandSamples??0)==0)return null;
            var cues=new List<string>();
            int last=result.Animation.Loop?result.Animation.FrameCount:result.Animation.FrameCount-1;
            string[] roles={"left.hand","right.hand"};
            var surfaces=roles.Select(role=>source.Rig.Surfaces.TryGetValue(role,out var surface)?surface:null).ToArray();
            var full=new int[roles.Length];var longest=new int[roles.Length];var run=new int[roles.Length];
            var first=Enumerable.Repeat(-1f,roles.Length).ToArray();var lastSupported=Enumerable.Repeat(-1f,roles.Length).ToArray();
            var minFlat=Enumerable.Repeat(1f,roles.Length).ToArray();var maxFlat=new float[roles.Length];
            if(result.Contacts!=null && result.Contacts.PlantedHandSamples>0)
            {
                for(int i=0;i<=last*4;i++)
                {
                    cancellation.ThrowIfCancellationRequested();
                    float time=i/4f;bool[] supported=roles.Select((role,index)=>surfaces[index]!=null&&
                        result.Contacts.At(role,time).Support>.99f).ToArray();
                    var pose=supported.Any(value=>value)?BrawlRetargeter.Sample(source,sourceClip,time):null;
                    for(int side=0;side<roles.Length;side++)
                    {
                        run[side]=supported[side]?run[side]+1:0;longest[side]=Math.Max(longest[side],run[side]);
                        if(!supported[side])continue;
                        full[side]++;if(first[side]<0)first[side]=time;lastSupported[side]=time;
                        float flat=surfaces[side].Sample(pose).Flatness;
                        minFlat[side]=Math.Min(minFlat[side],flat);maxFlat[side]=Math.Max(maxFlat[side],flat);
                    }
                }
            }
            for(int side=0;side<roles.Length;side++)
                if(full[side]>0)cues.Add("Proposed "+roles[side]+" support: "+full[side]+" full quarter-frame samples, longest run "+longest[side]+
                    ", frames "+Number(first[side])+"–"+Number(lastSupported[side])+", source patch flatness "+Number(minFlat[side])+"–"+Number(maxFlat[side])+
                    ". This is not an approved planted-hand contact; review the actual surface and floor context.");
            if(result.Plan!=null)AppendHandPoseReview(source,target,sourceClip,result,last,cues,cancellation);
            var mapped=result.Plan==null?new HashSet<int>():new HashSet<int>(result.Plan.Mapping.Select(m=>m.TargetIndex));
            var cranialRoots=new[]{"head","neck"}.Where(role=>target.Rig.Roles.ContainsKey(role))
                .Select(role=>target.Rig.Roles[role]).Distinct().ToArray();
            bool CranialDescendant(int joint)
            {
                for(int parent=joint;parent>=0;parent=target.Rig.Joints[parent].Parent)
                    if(cranialRoots.Contains(parent))return true;
                return false;
            }
            bool hasHeadJoint=target.Rig.Roles.TryGetValue("head",out int directHead);
            int[] targetOnlyCranial=result.Plan==null?new int[0]:Enumerable.Range(0,target.Rig.Joints.Length)
                .Where(joint=>(!hasHeadJoint||joint!=directHead)&&!mapped.Contains(joint)&&CranialDescendant(joint)).ToArray();
            if(result.Plan!=null && target.Rig.Surfaces.TryGetValue("head",out var head) && head.Points.Length>0)
            {
                long samples=(long)head.Points.Length*(last*4L+1);
                if(samples>MaximumSkinSamples)
                    cues.Add("Head-area skin guide inspection exceeded its "+MaximumSkinSamples.ToString(CultureInfo.InvariantCulture)+
                        "-point review budget. Clearance remains unverified; no pose was changed.");
                else
                {
                    float[] minima={float.PositiveInfinity,float.PositiveInfinity,float.PositiveInfinity};
                    float[] worst={-1,-1,-1};
                    string[] influences={null,null,null};
                    var classified=head.Points.Select(point=>
                    {
                        int group=ClassifyCranialSkinPoint(point,hasHeadJoint?directHead:-1,mapped,CranialDescendant);
                        return new {Point=point,Group=group,
                            Influence=string.Join(", ",point.Weights.Where(w=>w.Weight>0).OrderByDescending(w=>w.Weight)
                                .Take(3).Select(w=>target.Rig.Joints[w.Joint].Name+" "+Number(w.Weight)))};
                    }).ToArray();
                    for(int i=0;i<=last*4;i++)
                    {
                        cancellation.ThrowIfCancellationRequested();
                        float time=i/4f;
                        var pose=BrawlRetargeter.Sample(target,result.Animation,time);
                        foreach(var item in classified)
                        {
                            int group=item.Group;
                            float clearance=Vector3.Dot(item.Point.Evaluate(pose),target.Rig.GroundNormal)-target.Rig.GroundHeight;
                            if(!RigMath.Finite(clearance))throw new InvalidDataException("A sampled head-area skin point is non-finite.");
                            if(clearance>=minima[group])continue;
                            minima[group]=clearance;worst[group]=time;
                            influences[group]=item.Influence;
                        }
                    }
                    float leg=Vector3.Distance(target.Rig.Rest.Point("left.upperLeg"),target.Rig.Rest.Point("left.lowerLeg"))+
                        Vector3.Distance(target.Rig.Rest.Point("left.lowerLeg"),target.Rig.Rest.Point("left.foot"));
                    float roundoff=Math.Max(.00001f,leg*.0001f);
                    for(int group=0;group<3;group++)if(minima[group]<-roundoff)
                        cues.Add((group==0?"Direct head-joint skin":group==1?"Head-area skin with target-only descendant influence":"Other sampled cranial skin")+
                            " reaches "+Number(minima[group])+" native units relative to the target model's bind-sole guide at frame "+
                            Number(worst[group])+" ("+influences[group]+"). The guide is not a verified game floor or contact; review clearance. No automatic lift was applied.");
                }
                var represented=new HashSet<int>(head.Points.SelectMany(point=>point.Weights)
                    .Where(weight=>weight.Weight>0).Select(weight=>weight.Joint));
                int unrepresented=targetOnlyCranial.Count(joint=>!represented.Contains(joint));
                if(unrepresented>0)cues.Add(unrepresented+" target-only head/neck descendant joints are outside this sampled head-area skin patch. " +
                    "Attachment or costume clearance is unavailable until its exact geometry and context are reviewed.");
            }
            else if(targetOnlyCranial.Length>0)
                cues.Add("Target-only head/neck descendants exist, but a measurable head-area skin patch is unavailable. " +
                    "Attachment and floor clearance require separate review; no pose was changed.");
            return cues.Count==0?null:string.Join(Environment.NewLine,cues);
        }

        // A reviewed virtual source head deliberately has no target head FK
        // mapping. That does not turn the target's real head skin into an
        // accessory; only other unmapped cranial descendants belong there.
        internal static int ClassifyCranialSkinPoint(SkinPoint point,int directHead,
            ISet<int> mapped,Func<int,bool> cranialDescendant)
        {
            bool direct=directHead>=0&&point.Weights.Where(w=>w.Joint==directHead).Sum(w=>w.Weight)>=.5f;
            bool targetOnly=point.Weights.Any(w=>w.Weight>.01f&&w.Joint!=directHead&&
                !mapped.Contains(w.Joint)&&cranialDescendant(w.Joint));
            return targetOnly?1:direct?0:2;
        }

        // This observes the finished target animation, including its native
        // curve representation. A hand with no established contact can miss
        // the source pose even when the joint IK goal and supported-foot
        // checks pass. The bind-sole guides are only common measurement axes;
        // crossing one does not approve a floor, a pin, or a corrective lift.
        private static void AppendHandPoseReview(BrawlRig source,BrawlRig target,CHR0Node sourceClip,
            RetargetResult result,int last,List<string> cues,CancellationToken cancellation)
        {
            var pairs=new List<Tuple<string,ContactSurface,ContactSurface,float,float>>();
            foreach(string side in new[]{"left","right"})
            {
                string role=side+".hand";
                if(!source.Rig.Surfaces.TryGetValue(role,out var from)||
                    !target.Rig.Surfaces.TryGetValue(role,out var to)||
                    !new[]{side+".upperArm",side+".lowerArm",role}.All(name=>
                        source.Rig.Roles.ContainsKey(name)&&target.Rig.Roles.ContainsKey(name)))continue;
                float Length(Rig rig)=>Vector3.Distance(rig.Rest.Point(side+".upperArm"),rig.Rest.Point(side+".lowerArm"))+
                    Vector3.Distance(rig.Rest.Point(side+".lowerArm"),rig.Rest.Point(role));
                float sourceLength=Length(source.Rig),targetLength=Length(target.Rig);
                if(!RigMath.Finite(sourceLength)||!RigMath.Finite(targetLength)||sourceLength<=0||targetLength<=0)continue;
                pairs.Add(Tuple.Create(role,from,to,sourceLength,targetLength));
            }
            if(pairs.Count==0)return;
            long work=(last*4L+1)*pairs.Sum(pair=>(long)pair.Item2.Points.Length+pair.Item3.Points.Length);
            if(work>MaximumSkinSamples)
            {
                cues.Add("Hand-pose skin comparison exceeded its "+MaximumSkinSamples.ToString(CultureInfo.InvariantCulture)+
                    "-point review budget. Hand motion remains unverified; no pose was changed.");
                return;
            }
            var worst=pairs.Select(pair=>new{Pair=pair,Error=0f,Time=-1f,Source=0f,Target=0f,
                DeepestSource=0f,DeepestTime=-1f}).ToArray();
            for(int i=0;i<=last*4;i++)
            {
                cancellation.ThrowIfCancellationRequested();
                float time=i/4f;
                var from=BrawlRetargeter.Sample(source,sourceClip,time);
                var to=BrawlRetargeter.Sample(target,result.Animation,time);
                for(int side=0;side<pairs.Count;side++)
                {
                    var pair=pairs[side];
                    // Established source-derived support has its own contact
                    // checks; this cue is for the unreviewed hand-pose gap.
                    if(result.Contacts?.At(pair.Item1,time).Support>.99f)continue;
                    float sourceHeight=pair.Item2.Clearance(from)/pair.Item4;
                    float targetHeight=pair.Item3.Clearance(to)/pair.Item5;
                    if(!RigMath.Finite(sourceHeight)||!RigMath.Finite(targetHeight))
                        throw new InvalidDataException("A sampled hand-pose skin clearance is non-finite.");
                    float error=Math.Abs(sourceHeight-targetHeight);
                    var prior=worst[side];
                    worst[side]=new{Pair=pair,Error=error>prior.Error?error:prior.Error,
                        Time=error>prior.Error?time:prior.Time,
                        Source=error>prior.Error?sourceHeight:prior.Source,
                        Target=error>prior.Error?targetHeight:prior.Target,
                        DeepestSource=sourceHeight<prior.DeepestSource?sourceHeight:prior.DeepestSource,
                        DeepestTime=sourceHeight<prior.DeepestSource?time:prior.DeepestTime};
                }
            }
            foreach(var row in worst.Where(row=>row.Time>=0&&row.Error>.05f))
                cues.Add("Unreviewed "+row.Pair.Item1+" skin pose differs by "+Number(row.Error)+
                    " arm lengths at native frame "+Number(row.Time)+" (source "+Number(row.Source)+
                    ", target "+Number(row.Target)+" relative to their own bind-sole guides). "+
                    (row.DeepestTime>=0?"The source hand itself reaches "+Number(row.DeepestSource)+
                        " arm lengths below its guide at native frame "+Number(row.DeepestTime)+". ":"")+
                    "The pose gap exceeds the 0.05-arm review threshold; this is not a reviewed floor or planted contact. Review hand, forearm and shoulder motion; no automatic lift was applied.");
        }

        private static string Number(float value)=>value.ToString("0.####",CultureInfo.InvariantCulture);
    }
}
