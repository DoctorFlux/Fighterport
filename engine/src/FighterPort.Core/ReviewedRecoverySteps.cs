using System.Numerics;

namespace FighterPort.Core;

public static partial class ReviewedContactSets
{
    public const string RecoveryStepPolicy="reviewed-native-stance-steps-1";
    static bool RecoverySteps(ReviewedContactContext context)=>context.RecoveryPolicy==RecoveryStepPolicy;
    static string RecoveryRowsKey(ReviewedContactRange[] rows)=>ContentKey(rows.Select(r=>r with{Reviewed=false}).ToArray());
    static int Approach(ReviewedContactRange row)=>row.ApproachStart??row.Start-row.Blend;
    static int Release(ReviewedContactRange row)=>row.ReleaseEnd??row.End+row.Blend;
    static float ContactWeight(ReviewedContactRange row,double frame)
    {
        float t=frame<row.Start?(float)((frame-Approach(row))/Math.Max(1,row.Start-Approach(row))):
            frame>row.End?(float)((Release(row)-frame)/Math.Max(1,Release(row)-row.End)):1;
        t=Math.Clamp(t,0,1);return t*t*(3-2*t);
    }
    static float RecoveryFloor(Scene scene,AnimationClip master,ControlRigSetup rig,ReviewedContactContext context,ReviewedContactRange row)
    {
        if(!RecoverySteps(context))return context.Surface.SourceFloor!.Value;
        var normal=SurfaceContacts.Normal(scene,rig,context.Surface);var full=FullFootRegion(scene,row.Bone);
        int first=(int)context.FittingMiddle!.End,last=master.SampleCount-1;
        var a=ContactPose(scene,master,first);var b=ContactPose(scene,master,last);
        return Math.Min(context.Surface.SourceFloor!.Value,Math.Min(full.Min(p=>Vector3.Dot(ContactGeometry.Point(p,a),normal)),
            full.Min(p=>Vector3.Dot(ContactGeometry.Point(p,b),normal))));
    }
    public static ReviewedContactContext SuggestRecovery(FighterProject project,AnimationClip master,ControlRigSetup rig,
        GroundContactSettings surface,string[] protectedBones,ProtectedInterval middle,CancellationToken cancellation=default)
    {
        var context=Suggest(project,master,rig,surface,protectedBones,cancellation) with{Ranges=[],FittingMiddle=middle,RecoveryPolicy=RecoveryStepPolicy};
        var scene=project.Scene;int first=(int)middle.End,last=master.SampleCount-1,span=last-first;
        if(master.Loop||middle.Start!=0||middle.End!=first||first<1||span<7||span>MaximumEditedSamples||surface.SourceNormal is null)
            throw new NotSupportedException("Reviewed stance recovery requires an exact frozen prefix, explicit plane normal and at least seven exit intervals on a non-looping animation.");
        var feet=rig.Chains.Where(c=>c.Reviewed&&c.Role is "Foot.L" or "Foot.R").ToArray();
        if(feet.Length!=2||ReviewedContactBody(scene,rig)<0)throw new InvalidDataException("Review the existing body and both foot chains before stance recovery.");
        var normal=SurfaceContacts.Normal(scene,rig,surface);var a=ContactPose(scene,master,first);var b=ContactPose(scene,master,last);
        var entries=feet.Select(chain=>
        {
            var region=SurfaceContacts.Regions(scene,rig).Single(r=>r.Bone==chain.Effector);
            float length=chain.Calibration is {} scale?scale.UpperLength+scale.LowerLength:throw new InvalidDataException("Review foot calibration.");
            if(!float.IsFinite(length)||length<=0)throw new InvalidDataException("A finite reviewed leg scale is required.");
            var start=SurfaceContacts.Patch(region,a,normal,length*.006f);var end=SurfaceContacts.Patch(region,b,normal,length*.006f);
            var unique=start.Concat(end).Distinct().OrderBy(p=>p.Position.X).ThenBy(p=>p.Position.Y).ThenBy(p=>p.Position.Z).ToArray();
            var patch=unique.Length<=8?unique:Enumerable.Range(0,8).Select(i=>unique[i*(unique.Length-1)/7]).ToArray();
            Vector3 Centre(EvaluatedPose pose)=>patch.Aggregate(Vector3.Zero,(v,p)=>v+ContactGeometry.Point(p,pose))/patch.Length;
            float height=FullFootRegion(scene,chain.Effector).Min(p=>Vector3.Dot(ContactGeometry.Point(p,a),normal));
            return new{chain,region,patch,length,Height=height,First=Centre(a),Last=Centre(b),Travel=Vector3.Distance(Centre(a),Centre(b))};
        }).OrderByDescending(v=>v.Height-surface.SourceFloor!.Value).ThenByDescending(v=>v.Travel/v.length).ThenBy(v=>v.First.X).ToArray();
        int land=first+Math.Max(2,(int)Math.Round(span/3d)),depart=land,settle=last-2;
        if(settle-depart<3)throw new NotSupportedException("Recovery needs enough space for sequential reviewed foot steps.");
        var rows=new List<ReviewedContactRange>();
        ReviewedContactRange Row(int index,int start,int end,int anchor,int approach,int release)
        {
            var item=entries[index];var pose=anchor==first?a:b;
            var points=item.patch.Select(p=>ContactGeometry.Point(p,pose)).ToArray();var centre=points.Aggregate(Vector3.Zero,(v,p)=>v+p)/points.Length;
            return new(Guid.NewGuid().ToString("N"),item.chain.Role,item.chain.Effector,start,end,2,anchor,centre,
                PoseGeometry.Rotation(pose.World[item.chain.Effector]),item.patch,points,false)
                {Region=item.region.Points,Dependencies=FullFootDependencies(scene,item.chain.Effector),ApproachStart=approach,ReleaseEnd=release,NativeAnchorHeight=true};
        }
        // The higher/airborne foot lands first. The other foot keeps its exact
        // native starting anchor until that landing, then takes a separate step.
        float bound=entries.Min(v=>v.length)*.001f;
        bool Same(int i)=>entries[i].Travel<=bound&&ContactAuthoring.AngleError(PoseGeometry.Rotation(a.World[entries[i].chain.Effector]),PoseGeometry.Rotation(b.World[entries[i].chain.Effector]))<=.25f;
        rows.Add(Same(0)?Row(0,first,last,first,first,last):Row(0,land,last,last,first,last));
        bool same=Same(1);
        if(same)rows.Add(Row(1,first,last,first,first,last));
        else
        {
            int apex=depart+(settle-depart)/2;
            rows.Add(Row(1,first,depart,first,first,apex));
            rows.Add(Row(1,settle,last,last,apex,last));
        }
        context=context with{Ranges=rows.ToArray(),RecoveryPlanKey=RecoveryRowsKey(rows.ToArray()),Diagnostics=[..context.Diagnostics,
            "Reviewed recovery proposal: original prefix/join and final native pose stay exact; land the moving foot, then reposition the supporting foot if its target stance differs.",
            "Native anchor heights are retained. Per-foot clearance uses min(reviewed plane, exact starting sole, exact target sole); authored endpoint penetration is not erased.",
            "All holds, step phases, frozen anchors and complete deformation dependencies require review before Preview/Apply.",
            "Review replaces inferred plants on the selected feet throughout their hold/step window, including swing. Authored pins and protected motion controls are never released.",
            ..rows.Select(r=>r.Role+": hold display "+(r.Start+1)+"–"+(r.End+1)+", native anchor "+(r.AnchorFrame+1)+", approach/release "+(Approach(r)+1)+"/"+(Release(r)+1)+". Review the complete staged plan; adjust fitting recovery start and Suggest again to change timing.")]};
        ValidateContext(scene,master,rig,context,false);return context;
    }
    sealed record RecoveryPhase(ReviewedContactRange Patch,Vector3 Start,Vector3 End,Quaternion StartRotation,Quaternion EndRotation,
        int First,int Last,float Fraction,bool Hold);
    static RecoveryPhase RecoveryAt(Scene scene,AnimationClip master,ReviewedContactContext context,int foot,double frame)
    {
        var rows=context.Ranges.Where(r=>r.Reviewed&&r.Bone==foot).OrderBy(r=>r.Start).ToArray();
        var hold=rows.FirstOrDefault(r=>frame>=r.Start&&frame<=r.End);
        if(hold is not null)return new(hold,hold.Anchor,hold.Anchor,hold.Orientation,hold.Orientation,hold.Start,hold.End,1,true);
        var next=rows.FirstOrDefault(r=>frame<r.Start)??throw new InvalidDataException("The recovery step has no reviewed landing.");
        var prior=rows.LastOrDefault(r=>frame>r.End);int start=prior?.End??(int)context.FittingMiddle!.End;
        var pose=ContactPose(scene,master,start);var patch=next.Patch;
        var origin=prior?.Anchor??patch.Aggregate(Vector3.Zero,(v,p)=>v+ContactGeometry.Point(p,pose))/patch.Length;
        var rotation=prior?.Orientation??PoseGeometry.Rotation(pose.World[foot]);
        float u=Math.Clamp((float)((frame-start)/(next.Start-start)),0,1);u=u*u*(3-2*u);
        return new(next,origin,next.Anchor,rotation,next.Orientation,start,next.Start,u,false);
    }
    static ContactPin[] RecoveryGoals(Scene scene,AnimationClip master,ControlRigSetup rig,ReviewedContactContext context,double frame,Vector3 normal,
        IReadOnlyDictionary<string,SkinSurfacePoint[]> solverRegions,IReadOnlyDictionary<string,float> floors)
    {
        var goals=new List<ContactPin>();
        foreach(int foot in context.Ranges.Where(r=>r.Reviewed).Select(r=>r.Bone).Distinct())
        {
            var phase=RecoveryAt(scene,master,context,foot,frame);var row=phase.Patch;float u=phase.Fraction;
            var position=Vector3.Lerp(phase.Start,phase.End,u);var rotation=Quaternion.Slerp(phase.StartRotation,phase.EndRotation,u);
            float floor=floors[row.Id];
            if(!phase.Hold)
            {
                var chain=rig.Chains.Single(c=>c.Reviewed&&c.Effector==foot);
                float limb=chain.Calibration!.UpperLength+chain.Calibration.LowerLength;
                var approach=ContactGoal(scene,master,rig,context,row,frame,ContactPose(scene,master,(int)context.FittingMiddle!.End),normal,u,solverRegions[row.Id]);
                position=approach.Position;
                position+=normal*(limb*.025f*4*u*(1-u));
            }
            goals.Add(new(Guid.ParseExact(row.Id,"N"),row.Role,foot,Vector3.Zero,position,rotation,true,"Reviewed recovery hold/step")
                {SurfacePoints=row.Patch,Clearance=new(normal,floor,solverRegions[row.Id]){MaximumPenetration=.0002f}});
            if(phase.Hold)for(int i=0;i<row.Patch.Length;i++)
                goals.Add(new(Guid.ParseExact(ContentKey(new{row.Id,Point=i})[..32],"N"),row.Role,foot,Vector3.Zero,row.AnchorPoints[i],rotation,false,"Reviewed recovery patch")
                    {SurfacePoints=[row.Patch[i]]});
        }
        return goals.ToArray();
    }
    static void RecoveryShapeLocks(Scene scene,AnimationClip master,ControlRigSetup rig,ReviewedContactContext context,double frame,
        EvaluatedPose original,ISet<int> allowed,Dictionary<int,Transform> locks)
    {
        foreach(int foot in context.Ranges.Where(r=>r.Reviewed).Select(r=>r.Bone).Distinct())
        {
            var phase=RecoveryAt(scene,master,context,foot,frame);var row=phase.Patch;
            var a=ContactPose(scene,master,phase.Hold?row.AnchorFrame:(int)context.FittingMiddle!.End);
            var b=ContactPose(scene,master,row.AnchorFrame);
            foreach(int bone in ReviewedContactShapeBones(scene,rig,row,true).Where(b=>allowed.Contains(b)&&!locks.ContainsKey(b)))
            {
                var rotation=Quaternion.Slerp(PoseGeometry.Rotation(a.Local[bone].Matrix),PoseGeometry.Rotation(b.Local[bone].Matrix),phase.Fraction);
                locks[bone]=original.Local[bone] with{Rotation=AnimationControls.Euler(Matrix4x4.CreateFromQuaternion(rotation),original.Local[bone].Rotation)};
            }
        }
    }
}
