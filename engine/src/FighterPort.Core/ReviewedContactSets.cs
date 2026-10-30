using System.Numerics;
using System.Security.Cryptography;
using System.Text.Json;

namespace FighterPort.Core;

// Operation-owned evidence. These records contain no renderer/native resource
// owners. Suggestions are deliberately not approved constraints.
public sealed record ReviewedContactRange(string Id,string Role,int Bone,int Start,int End,int Blend,int AnchorFrame,
    Vector3 Anchor,Quaternion Orientation,SkinSurfacePoint[] Patch,Vector3[] AnchorPoints,bool Reviewed=false)
{
    public SkinSurfacePoint[] Region {get;init;}=[];
    public string[] Dependencies {get;init;}=[];
}
public sealed record ReviewedContactContext(string Version,string RigFingerprint,string MasterKey,
    GroundContactSettings Surface,ContactFrame[] OriginalContacts,ReviewedContactRange[] Ranges,
    string[] ProtectedBones,string[] Diagnostics)
{
    public string? OriginalSupportKey {get;init;}
    public InferredSupportWindow[] InferredSupports {get;init;}=[];
    public string InferredAdaptationPolicy {get;init;}="Reviewed ranges replace only the selected inferred foot support; all authored contacts remain exact.";
    public ProtectedInterval[] ProtectedIntervals {get;init;}=[];
    public string ShapeAdaptationPolicy {get;init;}="Reviewed toe rotations follow the frozen anchor shape at the contact weight; native scale, translation, helpers and unreviewed bones remain unchanged.";
    public string TrajectoryPolicy {get;init;}="Original-fidelity integer poses; subframes project continuous native-Euler corrections onto the same hard contacts.";
    public ProtectedInterval? FittingMiddle {get;init;}
    public int? EarlierBodyCorrectionStart {get;init;}
    public string EarlierCorrectionPolicy {get;init;}="Explicitly reviewed earlier body correction may adapt reviewed body dependencies of inferred supports; authored pins, protected motion and the chosen first frame remain exact.";
}
public sealed record InferredSupportWindow(string Role,string BoneName,float First,float Last,string[] Dependencies);
public sealed record ReviewedContactSetReceipt(string Version,string ContextKey,string MasterKey,int FirstChanged,int LastChanged,
    string[] ChangedBones,float PositionBound,float OrientationBound,double[] Witnesses,
    float MaximumDrift,float MaximumPenetration,float MaximumOrientationError,int PoseEvaluations,string[] Limits);
public sealed record ReviewedContactSetResult(AnimationClip Clip,BoneTrack[] Changed,ContactFrame[] Contacts,
    ReviewedContactContext Context,ReviewedContactSetReceipt Receipt);
public sealed record ReviewedContactRangeEdit(string? Id,string Role,int Start,int End,int Blend,int AnchorFrame,float[] Offset,bool Reviewed,bool Remove=false);

public static partial class ReviewedContactSets
{
    public const string Version="reviewed-contact-set-1";
    const string InferredPolicy="Reviewed ranges replace only the selected inferred foot support; all authored contacts remain exact.";
    const string ShapePolicy="Reviewed toe rotations follow the frozen anchor shape at the contact weight; native scale, translation, helpers and unreviewed bones remain unchanged.";
    const string TemporalPolicy="Original-fidelity integer poses; subframes project continuous native-Euler corrections onto the same hard contacts.";
    const string EarlierPolicy="Explicitly reviewed earlier body correction may adapt reviewed body dependencies of inferred supports; authored pins, protected motion and the chosen first frame remain exact.";
    public const int MaximumRanges=64,MaximumEditedSamples=240,MaximumKeys=2000000,EvaluationBudget=30000;
    // Project persistence omits computed read-only properties; anonymous frozen
    // dependency records need those properties included in their hash.
    public static string ContentKey<T>(T value)=>Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(value,
        new JsonSerializerOptions(ProjectStore.Json){IgnoreReadOnlyProperties=false}))).ToLowerInvariant();
    internal static string MasterKey(Scene scene,AnimationClip clip)=>ContentKey(new{scene.Bones,scene.Meshes,
        clip.Name,clip.SampleCount,clip.Rate,clip.Loop,clip.Tracks});

    public static ReviewedContactContext Suggest(FighterProject project,AnimationClip clip,ControlRigSetup rig,
        GroundContactSettings surface,string[] protectedBones,CancellationToken cancellation=default)
    {
        cancellation.ThrowIfCancellationRequested();
        if(rig.SkeletonFingerprint!=AnimationControls.Fingerprint(project.Scene))throw new InvalidDataException("Review the current native rig before suggesting contacts.");
        if(surface.SurfaceMode!=SupportSurfaceMode.Ground||!surface.ExplicitSurface||surface.SourceFloor is not {} floor||!float.IsFinite(floor))
            throw new InvalidOperationException("Review a finite static ground plane first. Ledge and moving supports require their own context.");
        if(clip.SampleCount<3)throw new NotSupportedException("Contact transitions need at least three native samples.");
        var original=project.Contacts.Where(c=>c.Clip==clip.Id).ToArray();
        if(original.Any(c=>c.SkeletonFingerprint!=rig.SkeletonFingerprint))throw new InvalidDataException("Saved contacts belong to another rig revision.");
        var analysis=SurfaceContacts.Detect(project,clip,rig,surface,cancellation);
        var up=RigCharacterization.Describe(project.Scene,rig).Basis;
        var normal=new Vector3(up.M21,up.M22,up.M23);
        var ranges=new List<ReviewedContactRange>();
        foreach(var interval in analysis.Contacts.OrderBy(c=>c.Frame))foreach(var pin in interval.Pins)
        {
            cancellation.ThrowIfCancellationRequested();
            if(!rig.Chains.Any(c=>c.Reviewed&&c.Effector==pin.Bone&&c.Role.StartsWith("Foot.",StringComparison.Ordinal)))continue;
            if(pin.Mode!=ContactMode.Plant||pin.EdgeSupport||pin.Reference is not null||pin.EditorMotion is not null)continue;
            int start=Math.Max(2,(int)Math.Ceiling(interval.Frame)),end=Math.Min(clip.SampleCount-3,(int)Math.Floor(interval.EndFrame??interval.Frame));
            int blend=Math.Min(4,Math.Min(start,clip.SampleCount-1-end));
            if(blend<2||end<start||end-start+2*blend>MaximumEditedSamples)continue;
            var region=analysis.Regions.SingleOrDefault(r=>r.Bone==pin.Bone);
            var patch=pin.SurfacePoints.Length>0?pin.SurfacePoints:region?.Points??[];
            if(patch.Length==0)continue;
            var pose=ContactPose(project.Scene,clip,start);
            var points=patch.Select(p=>ContactGeometry.Point(p,pose)).ToArray();
            var centre=points.Aggregate(Vector3.Zero,(a,p)=>a+p)/points.Length;
            var offset=normal*(floor-points.Min(p=>Vector3.Dot(p,normal)));
            ranges.Add(new(Guid.NewGuid().ToString("N"),pin.Role,pin.Bone,start,end,blend,start,
                centre+offset,PoseGeometry.Rotation(pose.World[pin.Bone]),patch.ToArray(),points.Select(p=>p+offset).ToArray())
                {Region=region!.Points.ToArray(),Dependencies=FullFootDependencies(project.Scene,pin.Bone)});
            if(ranges.Count>MaximumRanges)throw new InvalidOperationException("Contact suggestions exceed the existing bounded review capacity; choose a shorter animation.");
        }
        return new(Version,rig.SkeletonFingerprint,MasterKey(project.Scene,clip),surface,original,ranges.ToArray(),
            protectedBones.Distinct(StringComparer.Ordinal).ToArray(),analysis.Diagnostics.Select(d=>d.Message).Concat(new[]{
                "Suggestions require explicit review; no constraint has been approved or animation changed.",
                "Only stationary foot ranges with room for approach and release are suggested. Rolling, airborne and uncertain intervals remain unapproved."}).ToArray());
    }

    public static ReviewedContactContext SetRange(Scene scene,AnimationClip master,ControlRigSetup rig,ReviewedContactContext context,
        string role,int start,int end,int blend,int anchorFrame,Vector3 offset,bool reviewed,string? id=null)
    {
        ValidateContext(scene,master,rig,context,false);
        if(!PoseGeometry.Finite(offset)||anchorFrame<0||anchorFrame>=master.SampleCount)throw new InvalidDataException("A reviewed anchor requires a finite offset and native frame.");
        var chain=rig.Chains.SingleOrDefault(c=>c.Reviewed&&c.Role==role&&c.Role.StartsWith("Foot.",StringComparison.Ordinal))
            ??throw new InvalidDataException("Choose a reviewed existing foot chain.");
        var region=SurfaceContacts.Regions(scene,rig).Single(r=>r.Bone==chain.Effector);
        var anatomy=RigCharacterization.Describe(scene,rig);var normal=new Vector3(anatomy.Basis.M21,anatomy.Basis.M22,anatomy.Basis.M23);
        if(Math.Abs(Vector3.Dot(offset,normal))>.00001f)throw new InvalidDataException("A floor-contact anchor offset must stay in the reviewed ground plane. Change the reviewed ground height for another surface.");
        var pose=ContactPose(scene,master,anchorFrame);var patch=SurfaceContacts.Patch(region,pose,normal,anatomy.LegLength*.006f);
        var points=patch.Select(p=>ContactGeometry.Point(p,pose)).ToArray();
        var shift=normal*(context.Surface.SourceFloor!.Value-points.Min(p=>Vector3.Dot(p,normal)))+offset;
        var centre=points.Aggregate(Vector3.Zero,(a,p)=>a+p)/points.Length;
        var row=new ReviewedContactRange(id??Guid.NewGuid().ToString("N"),role,chain.Effector,start,end,blend,anchorFrame,centre+shift,
            PoseGeometry.Rotation(pose.World[chain.Effector]),patch,points.Select(p=>p+shift).ToArray(),reviewed){Region=region.Points,Dependencies=FullFootDependencies(scene,chain.Effector)};
        var result=context with{Ranges=context.Ranges.Where(r=>r.Id!=row.Id).Append(row).ToArray()};
        ValidateContext(scene,master,rig,result,false);return result;
    }

    public static void ValidateContext(Scene scene,AnimationClip master,ControlRigSetup rig,ReviewedContactContext context,bool requireReview)
    {
        if(context.Version!=Version||context.RigFingerprint!=AnimationControls.Fingerprint(scene)||context.RigFingerprint!=rig.SkeletonFingerprint||
            context.MasterKey!=MasterKey(scene,master))throw new InvalidDataException("The animation, rig or contact baseline changed. Suggest and review again.");
        if(context.Surface.SurfaceMode!=SupportSurfaceMode.Ground||!context.Surface.ExplicitSurface||context.Surface.SourceFloor is not {} floor||!float.IsFinite(floor))
            throw new InvalidDataException("A reviewed static ground surface is required.");
        if(context.InferredAdaptationPolicy!=InferredPolicy||context.ShapeAdaptationPolicy!=ShapePolicy||context.TrajectoryPolicy!=TemporalPolicy||context.EarlierCorrectionPolicy!=EarlierPolicy)
            throw new InvalidDataException("Unknown contact adaptation policy; review with a supported context version.");
        if(context.Ranges.Length>MaximumRanges||requireReview&&!context.Ranges.Any(r=>r.Reviewed))throw new InvalidDataException("Review at least one contact range.");
        if(context.Ranges.Select(r=>r.Id).Distinct(StringComparer.Ordinal).Count()!=context.Ranges.Length)throw new InvalidDataException("Contact range identities must be unique.");
        var motion=AnimationControls.ProtectedMotionBones(scene,rig).ToHashSet();
        foreach(var row in context.Ranges)
        {
            if(!Guid.TryParseExact(row.Id,"N",out _)||row.Bone<0||row.Bone>=scene.Bones.Length||motion.Contains(row.Bone)||row.Start<0||row.End<row.Start||row.Blend<2||
                row.Start-row.Blend<0||row.End+row.Blend>=master.SampleCount||row.End-row.Start+2*row.Blend>MaximumEditedSamples||
                row.AnchorFrame<0||row.AnchorFrame>=master.SampleCount||!PoseGeometry.Finite(row.Anchor)||!float.IsFinite(row.Orientation.LengthSquared())||row.Orientation.LengthSquared()<.0001f||
                row.Patch.Length==0||row.Region.Length==0||row.Patch.Length!=row.AnchorPoints.Length||row.AnchorPoints.Any(p=>!PoseGeometry.Finite(p))||
                !rig.Chains.Any(c=>c.Reviewed&&c.Effector==row.Bone&&c.Role==row.Role&&c.Role.StartsWith("Foot.",StringComparison.Ordinal)))
                throw new InvalidDataException("A contact range lost its reviewed existing foot chain, finite patch/anchor or native approach/hold/release interval.");
            var actualRegion=SurfaceContacts.Regions(scene,rig).SingleOrDefault(r=>r.Bone==row.Bone);
            if(actualRegion is null||ContentKey(actualRegion.Points)!=ContentKey(row.Region))
                throw new InvalidDataException("The complete native foot region or its skin influences changed.");
            var nativePoints=actualRegion.Points.Select(ContentKey).ToHashSet(StringComparer.Ordinal);
            if(row.Patch.Any(p=>!nativePoints.Contains(ContentKey(p))))throw new InvalidDataException("A contact patch contains points outside the reviewed native foot region.");
            if(!row.Dependencies.SequenceEqual(FullFootDependencies(scene,row.Bone),StringComparer.Ordinal))
                throw new InvalidDataException("A reviewed contact lost complete native ancestor/skin-influence dependencies.");
            var anchorPose=ContactPose(scene,master,row.AnchorFrame);
            var nativePatch=row.Patch.Select(p=>ContactGeometry.Point(p,anchorPose)).ToArray();
            var center=nativePatch.Aggregate(Vector3.Zero,(a,p)=>a+p)/nativePatch.Length;
            var shift=row.Anchor-center;
            var basis=RigCharacterization.Describe(scene,rig).Basis;
            var normal=new Vector3(basis.M21,basis.M22,basis.M23);
            if(row.AnchorPoints.Where((point,i)=>Vector3.Distance(point,nativePatch[i]+shift)>.00001f).Any()||
                Math.Abs(row.AnchorPoints.Min(p=>Vector3.Dot(p,normal))-floor)>.00001f)
                throw new InvalidDataException("A ground-contact patch must retain its native shape and touch the reviewed floor.");
        }
        foreach(var group in context.Ranges.Where(r=>r.Reviewed).GroupBy(r=>r.Bone))
        {
            var rows=group.OrderBy(r=>r.Start).ToArray();
            for(int i=1;i<rows.Length;i++)if(rows[i].Start-rows[i].Blend<=rows[i-1].End+rows[i-1].Blend)
                throw new InvalidOperationException("Overlapping contacts on the same foot conflict. Different feet are solved together.");
        }
        if(context.ProtectedBones.Any(name=>!scene.Bones.Any(b=>b.Name==name)))throw new InvalidDataException("A protected contact dependency is missing from the target.");
        if(context.ProtectedIntervals.Length>256||context.ProtectedIntervals.Any(p=>!double.IsFinite(p.Start)||!double.IsFinite(p.End)||p.Start<0||p.End<p.Start||p.End>master.SampleCount-1))
            throw new InvalidDataException("Invalid protected fitting/action interval.");
        if(context.FittingMiddle is {} middle&&(!double.IsFinite(middle.Start)||!double.IsFinite(middle.End)||middle.Start<0||middle.End<middle.Start||middle.End>master.SampleCount-1))
            throw new InvalidDataException("Invalid retained fitting middle.");
        if(context.EarlierBodyCorrectionStart is {} earlier&&(earlier<0||!context.Ranges.Any(r=>r.Reviewed)||earlier>=context.Ranges.Where(r=>r.Reviewed).Min(r=>r.Start-r.Blend)))
            throw new InvalidDataException("Review an earlier body-correction first frame before contact establishment; its pose remains exact.");
        if(context.InferredSupports.Length>20000||context.InferredSupports.Length>0&&string.IsNullOrEmpty(context.OriginalSupportKey))throw new InvalidDataException("Missing complete original support evidence identity.");
        foreach(var support in context.InferredSupports)if(!float.IsFinite(support.First)||!float.IsFinite(support.Last)||support.First<0||support.Last<support.First||support.Last>master.SampleCount||
            support.Dependencies.Length==0||!scene.Bones.Any(b=>b.Name==support.BoneName)||support.Dependencies.Any(name=>!scene.Bones.Any(b=>b.Name==name)))
                throw new InvalidDataException("The exact original inferred support timeline lost a native dependency.");
    }
}
