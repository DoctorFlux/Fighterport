using System.Numerics;
using System.Text.Json.Serialization;

namespace FighterPort.Core;

// Bind-space skin samples, not replacement bones. The constraint evaluates the same
// weighted surface as the viewport, including blended pelvis/torso/palm vertices.
public record SkinSurfacePoint(Vector3 Position,Vector4 Joints,Vector4 Weights)
{
    [JsonIgnore] public SkinInfluence[] Influences {get;init;}=[];
    [JsonPropertyName("Influences"),JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingNull)]
    public SkinInfluence[]? InfluenceData {get=>Influences.Length==0?null:Influences;init=>Influences=value??[];}
    [System.Text.Json.Serialization.JsonIgnore] public int InfluenceCount => SkinInfluences.Count(Influences);
    public float JointAt(int slot) => SkinInfluences.Joint(Joints,Influences,slot);
    public float WeightAt(int slot) => SkinInfluences.Weight(Weights,Influences,slot);
    public int Mesh {get;init;}=-1;
    public int Vertex {get;init;}=-1;
}
public record ContactClearance(Vector3 Normal,float Height,SkinSurfacePoint[] Points)
{
    public SkinSurfacePoint[] SupportPoints {get;init;}=[];
    [System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public SupportEdge? Edge {get;init;}
    [System.Text.Json.Serialization.JsonIgnore]
    public SupportSurface Surface=>new(Normal,Height,Edge);
}
public enum SupportSurfaceMode { Automatic, Ground, Edge, Free }
public record SupportSurfacePhase(double Start,double End,SupportSurfaceMode Mode);
public record GroundContactSettings
{
    public SupportSurfaceMode SurfaceMode {get;init;}=SupportSurfaceMode.Automatic;
    public float HeightFraction {get;init;}=.005f;
    public float? LimbHeightFraction {get;init;}
    public float DriftFraction {get;init;}=.035f;
    public float SpeedFractionPerSecond {get;init;}=.5f;
    public double MinimumSeconds {get;init;}=.05;
    public float? SourceFloor {get;init;}
    public float? TargetFloor {get;init;}
    [System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingDefault)]
    public bool ExplicitSurface {get;init;}
    [System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public SupportEdge? SourceEdge {get;init;}
    [System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public SupportEdge? TargetEdge {get;init;}
}
public record SupportRegion(string Role,int Bone,SkinSurfacePoint[] Points);
public record GroundContactAnalysis(float Floor,SupportRegion[] Regions,ContactFrame[] Contacts,Diagnostic[] Diagnostics)
{
    public SupportSurfacePhase[] Phases {get;init;}=[];
    public bool BodySupportedThroughout {get;init;}
    public SupportSurfacePhase[] BodySupportPhases {get;init;}=[];
    public bool BodySupportedAtClosing {get;init;}
}
public record GroundContactReport(string Version,float SourceFloor,float TargetFloor,int DetectedIntervals,string[] Regions,float MaximumPenetration)
{
    [System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingDefault)]
    public bool ExplicitSurface {get;init;}
    [System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public SupportEdge? SourceEdge {get;init;}
    [System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public SupportEdge? TargetEdge {get;init;}
    public SupportSurfacePhase[] Phases {get;init;}=[];
    public bool BodySupportedThroughout {get;init;}
    public SupportSurfacePhase[] BodySupportPhases {get;init;}=[];
    public bool BodySupportedAtClosing {get;init;}
}

public static class ContactGeometry
{
    public static Vector3 Target(ContactPin pin,EvaluatedPose pose)
    {
        var local=pin.Socket is {} socket?pin.Reference?.Grips.SingleOrDefault(g=>g.Socket==socket)?.Position??throw new InvalidOperationException("Missing reviewed shared-object grip socket: "+socket):pin.Position;
        return pin.Reference is {} reference?Vector3.Transform(local,reference.Evaluate(pose)):local;
    }
    public static Quaternion TargetRotation(ContactPin pin,EvaluatedPose pose)
    {
        var local=pin.Socket is {} socket?pin.Reference?.Grips.SingleOrDefault(g=>g.Socket==socket)?.Rotation??throw new InvalidOperationException("Missing reviewed grip orientation: "+socket):pin.Rotation;
        return pin.Reference is {} reference?Quaternion.Normalize(Quaternion.CreateFromRotationMatrix(Matrix4x4.CreateFromQuaternion(local)*reference.Evaluate(pose))):local;
    }
    public static Vector3? Normal(ContactPin pin,EvaluatedPose pose)=>pin.PlaneNormal is {} normal?pin.Reference is {} reference?Vector3.Normalize(Vector3.TransformNormal(normal,reference.Evaluate(pose))):normal:null;
    public static Vector3 Point(SkinSurfacePoint point,EvaluatedPose pose)
    {
        var position=pose.GeometryPositions is {} geometry&&point.Mesh>=0&&point.Mesh<geometry.Length&&point.Vertex>=0&&point.Vertex<geometry[point.Mesh].Length?geometry[point.Mesh][point.Vertex]:point.Position;
        var value=Vector3.Zero;for(int j=0;j<point.InfluenceCount;j++)if(point.WeightAt(j)>0)value+=Vector3.Transform(position,pose.Skin[(int)point.JointAt(j)])*point.WeightAt(j);return value;
    }
    public static Vector3 Point(ContactPin pin,EvaluatedPose pose)
    {
        if(pin.SurfaceRegion.Length>0)
        {
            if(AllowedSurface(pin,pose) is {} support)return pin.SurfaceRegion.Select(p=>Point(p,pose)).MinBy(p=>support.TopOffset(p).LengthSquared());
            if(pin.EdgeInward is {} inward&&Normal(pin,pose) is {} up)
            {
                var goal=Target(pin,pose);
                return pin.SurfaceRegion.Select(p=>Point(p,pose)).OrderBy(p=>MathF.Pow(Vector3.Dot(p-goal,up),2)+MathF.Pow(Vector3.Dot(p-goal,inward),2)).First();
            }
            var normal=Normal(pin,pose)??Vector3.UnitY;float lowest=float.PositiveInfinity;var sum=Vector3.Zero;int count=0;
            foreach(var sample in pin.SurfaceRegion){var point=Point(sample,pose);float height=Vector3.Dot(point,normal);if(height<lowest-.00001f){lowest=height;sum=point;count=1;}else if(height<=lowest+.00001f){sum+=point;count++;}}
            return sum/Math.Max(1,count);
        }
        return pin.SurfacePoints.Length==0?Vector3.Transform(pin.LocalPoint,pose.World[pin.Bone]):pin.SurfacePoints.Aggregate(Vector3.Zero,(p,s)=>p+Point(s,pose))/pin.SurfacePoints.Length;
    }
    public static SkinSurfacePoint SupportPoint(SkinSurfacePoint[] region,EvaluatedPose pose,SupportSurface surface,float positionBound=0)
    {
        // Support belongs to the exposed underside over the platform. Choosing
        // the nearest-to-zero vertex could instead plant the TOP of a buried
        // boot/palm. Exterior hanging skin is not part of the top footprint.
        // Include a boundary witness only when its FULL distance to the top
        // meets the same frozen position bound. A deep hanging vertex just
        // outside the wall must not enter through a horizontal-only margin.
        var footprint=region.Where(p=>surface.AboveTop(Point(p,pose))||surface.TopOffset(Point(p,pose)).Length()<=positionBound).ToArray();
        return footprint.Length>0?footprint.MinBy(p=>Vector3.Dot(Point(p,pose),surface.Normal))!:region.MinBy(p=>surface.TopOffset(Point(p,pose)).LengthSquared())!;
    }
    public static Vector3 Residual(ContactPin pin,EvaluatedPose pose)
    {
        if(pin.ClearanceOnly)return Vector3.Zero;
        var delta=Point(pin,pose)-Target(pin,pose);
        if(AllowedSurface(pin,pose) is {} support)
        {
            if(Normal(pin,pose) is not null)return support.TopOffset(Point(pin,pose));
            // Keep fixed-anchor and region errors separate. Adding signed
            // vectors could cancel them and silently release an authored pin.
            return delta;
        }
        if(pin.EdgeInward is {} inward&&Normal(pin,pose) is {} up)return up*Vector3.Dot(delta,up)+inward*Vector3.Dot(delta,inward);
        return Normal(pin,pose) is {} n?n*Vector3.Dot(delta,n):delta;
    }
    public static Vector3 AllowedSurfaceResidual(ContactPin pin,EvaluatedPose pose)=>AllowedSurface(pin,pose) is {} support?support.TopOffset(Point(pin,pose)):Vector3.Zero;
    // Independent acceptance also checks the exposed underside. The optimizer's
    // nearest-patch contact plus full-skin nonpenetration has the same feasible
    // set, but a buried candidate must not report zero support separation.
    public static float SupportSeparation(ContactPin pin,EvaluatedPose pose,float positionBound=0)
    {
        float error=Math.Max(Residual(pin,pose).Length(),AllowedSurfaceResidual(pin,pose).Length());
        return pin.SurfaceRegion.Length>0&&AllowedSurface(pin,pose) is {} support?Math.Max(error,support.TopOffset(Point(SupportPoint(pin.SurfaceRegion,pose,support,positionBound),pose)).Length()):error;
    }
    public static SupportSurface? AllowedSurface(ContactPin pin,EvaluatedPose pose)
    {
        if(pin.AllowedSurface is not {} local)return null;if(pin.Reference is not {} reference)return local;
        var frame=reference.Evaluate(pose);var normal=Vector3.Normalize(Vector3.TransformNormal(local.Normal,frame));float height=Vector3.Dot(Vector3.Transform(local.Normal*local.Height,frame),normal);
        var edge=local.Edge is {} e?new SupportEdge(Vector3.Normalize(Vector3.TransformNormal(e.Inward,frame)),0):null;
        if(edge is not null)edge=edge with{Offset=Vector3.Dot(Vector3.Transform(local.Edge!.Inward*local.Edge.Offset,frame),edge.Inward)};
        return new(normal,height,edge);
    }
    public static float Penetration(ContactPin pin,EvaluatedPose pose)=>pin.Clearance is {} plane?plane.Points.Select(p=>plane.Surface.Penetration(Point(p,pose))).DefaultIfEmpty().Max():0;
    public static float SupportHeight(ContactClearance plane,EvaluatedPose pose)=>(plane.SupportPoints.Length>0?plane.SupportPoints:plane.Points).Min(p=>Vector3.Dot(Point(p,pose),plane.Normal))-plane.Height;
    public static bool Valid(ContactPin pin,int bones)=>pin.SurfacePoints.Length<=8&&pin.SurfacePoints.All(s=>Valid(s,bones))&&pin.SurfaceRegion.Length<=512&&pin.SurfaceRegion.All(s=>Valid(s,bones))&&(pin.SurfaceRegion.Length==0||pin.PlaneNormal is not null)&&(pin.PlaneNormal is not {} n||PoseGeometry.Finite(n)&&Math.Abs(n.LengthSquared()-1)<.001f)&&(pin.EdgeInward is not {} edge||pin.EdgeSupport&&pin.Reference is null&&pin.PlaneNormal is {} normal&&PoseGeometry.Finite(edge)&&Math.Abs(edge.LengthSquared()-1)<.001f&&Math.Abs(Vector3.Dot(edge,normal))<.001f)&&(pin.AllowedSurface is not {} surface||Valid(surface)&&pin.EdgeInward is null&&(pin.PlaneNormal is not {} planeNormal||Vector3.Dot(planeNormal,surface.Normal)>.9999f))&&(pin.Clearance is not {} c||PoseGeometry.Finite(c.Normal)&&Math.Abs(c.Normal.LengthSquared()-1)<.001f&&float.IsFinite(c.Height)&&c.Points.Length<=512&&c.Points.All(p=>Valid(p,bones))&&c.SupportPoints.Length<=512&&c.SupportPoints.All(p=>Valid(p,bones)));
    public static bool Valid(SupportSurface surface)=>PoseGeometry.Finite(surface.Normal)&&Math.Abs(surface.Normal.LengthSquared()-1)<.0001f&&float.IsFinite(surface.Height)&&(surface.Edge is not {} e||PoseGeometry.Finite(e.Inward)&&float.IsFinite(e.Offset)&&Math.Abs(e.Inward.LengthSquared()-1)<.0001f&&Math.Abs(Vector3.Dot(e.Inward,surface.Normal))<.0001f);
    public static bool Valid(SkinSurfacePoint point,int bones)=>PoseGeometry.Finite(point.Position)&&SkinInfluences.Valid(point.Joints,point.Weights,point.Influences,bones)&&Math.Abs(Enumerable.Range(0,point.InfluenceCount).Sum(i=>(double)point.WeightAt(i))-1)<.002;
}

public static class SurfaceContacts
{
    public const string Version="functional-surface-relations-21";
    // One frozen detector policy for every rig. These classify candidate
    // evidence; the much stricter contact acceptance bounds remain separate.
    public const float DefaultEffectorBandFraction=.02f;
    public static bool BodySupportedAt(SupportSurfacePhase[] phases,bool throughout,bool closing,double frame,int sampleCount)
        =>Math.Abs(frame-sampleCount)<1e-8?closing:phases.Length==0?throughout:phases.Any(p=>frame>=p.Start&&frame<p.End+1);
    internal static ContactFrame[] SplitBodyPhases(ContactFrame[] contacts,GroundContactAnalysis? ground)
    {
        if(ground is null||ground.BodySupportPhases.Length==0)return contacts;
        var boundaries=ground.BodySupportPhases.SelectMany(p=>new[]{p.Start,p.End+1}).Distinct().Order().ToArray();
        return contacts.SelectMany(c=>
        {
            if(!c.Pins.Any(p=>p.Automatic&&!p.RequiredSupport))return new[]{c};
            var starts=boundaries.Where(f=>f>c.Frame&&f<(c.EndFrame??c.Frame)).Prepend(c.Frame).ToArray();
            return starts.Select((start,i)=>c with{Id=i==0?c.Id:Guid.NewGuid(),Frame=start,EndFrame=i+1<starts.Length?starts[i+1]:c.EndFrame,EndExclusive=i+1<starts.Length||c.EndExclusive}).ToArray();
        }).ToArray();
    }
    public static SupportRegion[] Regions(Scene scene,ControlRigSetup rig)
    {
        var anatomy=RigCharacterization.Describe(scene,rig);
        bool Support(string role)=>role is "Hips" or "Spine" or "Chest" or "Neck" or "Head"||new[]{"UpperArm.","Elbow.","Wrist.","UpperLeg.","Knee.","Ankle.","Toe.","Clavicle."}.Any(role.StartsWith);
        var owners=anatomy.Roles.Where(p=>Support(p.Key)).GroupBy(p=>p.Value).ToDictionary(g=>g.Key,g=>g.First().Key);
        foreach(var chain in rig.Chains)owners[chain.Effector]=chain.Role;
        // Toe bones remain native FK channels, but heel/toe skin forms one functional sole.
        foreach(var foot in rig.Chains.Where(c=>c.Role.StartsWith("Foot.",StringComparison.Ordinal)))foreach(int bone in owners.Keys.Where(i=>i!=foot.Effector&&PoseGeometry.Ancestor(scene,foot.Effector,i)).ToArray())owners.Remove(bone);
        var groups=owners.Keys.ToDictionary(i=>i,_=>new HashSet<SkinSurfacePoint>());
        foreach(var (mesh,meshIndex) in scene.Meshes.Select((m,i)=>(m,i)))foreach(var (vertex,vertexIndex) in mesh.Vertices.Select((v,i)=>(v,i)))
        {
            var weights=new Dictionary<int,float>();for(int j=0;j<vertex.InfluenceCount;j++)if(vertex.WeightAt(j)>0)
            {
                int bone=(int)vertex.JointAt(j);while(bone>=0&&!owners.ContainsKey(bone))bone=scene.Bones[bone].Parent;
                if(bone>=0)weights[bone]=weights.GetValueOrDefault(bone)+vertex.WeightAt(j);
            }
            if(weights.Count==0)continue;var owner=weights.MaxBy(p=>p.Value);if(owner.Value<.5f)continue;
            var point=new SkinSurfacePoint(vertex.Position,vertex.Joints,vertex.Weights){Influences=vertex.Influences,Mesh=meshIndex,Vertex=vertexIndex};if(ContactGeometry.Valid(point,scene.Bones.Length))groups[owner.Key].Add(point);
        }
        // Preserve spatial extrema when bounding a dense region; never select by bone name.
        SkinSurfacePoint[] Bound(HashSet<SkinSurfacePoint> points)
        {
            var ordered=points.OrderBy(p=>p.Position.X).ThenBy(p=>p.Position.Y).ThenBy(p=>p.Position.Z).ToArray();if(ordered.Length<=512)return ordered;
            var chosen=new HashSet<SkinSurfacePoint>();foreach(var axis in new[]{Vector3.UnitX,Vector3.UnitY,Vector3.UnitZ}){chosen.Add(ordered.MinBy(p=>Vector3.Dot(p.Position,axis))!);chosen.Add(ordered.MaxBy(p=>Vector3.Dot(p.Position,axis))!);}
            for(int i=0;i<506;i++)chosen.Add(ordered[(int)((long)i*(ordered.Length-1)/505)]);return chosen.ToArray();
        }
        return groups.Where(p=>p.Value.Count>=3).OrderBy(p=>owners[p.Key],StringComparer.Ordinal).Select(p=>new SupportRegion(owners[p.Key],p.Key,Bound(p.Value))).ToArray();
    }
    public static float Floor(Scene scene,ControlRigSetup rig,SupportRegion[] regions,Vector3 up)
    {
        if(rig.GroundPlaneHeight is {} reviewed)return reviewed;
        var feet=rig.Chains.Where(c=>c.Role.StartsWith("Foot.",StringComparison.Ordinal)).Select(c=>c.Effector).ToHashSet();
        var points=regions.Where(r=>feet.Any(f=>PoseGeometry.Ancestor(scene,f,r.Bone))).SelectMany(r=>r.Points).ToArray();
        if(points.Length<3)throw new InvalidOperationException("Ground calibration needs weighted sole geometry on the reviewed foot regions; specify/review a support profile for this rig.");
        var rest=AnimationService.Evaluate(scene,null,0);return points.Min(p=>Vector3.Dot(ContactGeometry.Point(p,rest),up));
    }
    public static SkinSurfacePoint[] Patch(SupportRegion region,EvaluatedPose pose,Vector3 up,float thickness)
    {
        var ordered=region.Points.Select(p=>(Point:p,Height:Vector3.Dot(ContactGeometry.Point(p,pose),up))).OrderBy(p=>p.Height).ToArray();
        var low=ordered.Where(p=>p.Height<=ordered[0].Height+Math.Min(thickness,.00001f)).Select(p=>p.Point).ToArray();
        // A small actual surface patch retains weighted skin behavior without a per-frame mesh solve.
        return low.Length<=8?low:Enumerable.Range(0,8).Select(i=>low[i*(low.Length-1)/7]).ToArray();
    }
    // A ledge is supported at its corner, not at the lowest part of a hand.
    // Selecting a sole-style patch here mistakes rolling fingers for fast motion
    // and can miss the entire initial support interval.
    public static SkinSurfacePoint[] EdgePatch(SupportRegion region,EvaluatedPose pose,SupportSurface surface)
    {
        if(surface.Edge is not {} edge)throw new ArgumentException("A corner patch requires supplied ledge geometry.");
        return region.Points.Select(p=>{var v=ContactGeometry.Point(p,pose);float y=Vector3.Dot(v,surface.Normal)-surface.Height,z=Vector3.Dot(v,edge.Inward)-edge.Offset;return(Point:p,Distance:y*y+z*z);})
            .OrderBy(p=>p.Distance).ThenBy(p=>p.Point.Mesh).ThenBy(p=>p.Point.Vertex).Take(1).Select(p=>p.Point).ToArray();
    }
    // Classify the sampled relationship to supplied geometry independently of
    // the body's height or the identity of the region. A corner contact can
    // become a top contact while the rest of the body still hangs below it.
    public static (SkinSurfacePoint[] Patch,bool Corner,float Separation) SupportPatch(SupportRegion region,EvaluatedPose pose,SupportSurface surface,float band)
    {
        if(surface.Edge is null)
        {
            // An infinite floor supports the exposed underside. A vertex near
            // zero inside an otherwise buried region is not floor support.
            var underside=Patch(region,pose,surface.Normal,.00001f);
            float height=underside.Average(p=>Vector3.Dot(ContactGeometry.Point(p,pose),surface.Normal));
            return(underside,false,Math.Abs(height-surface.Height));
        }
        var top=region.Points.MinBy(p=>surface.TopOffset(ContactGeometry.Point(p,pose)).LengthSquared())!;
        if(surface.Edge is {} edge)
        {
            var corner=EdgePatch(region,pose,surface);var point=ContactGeometry.Point(corner[0],pose);
            float distance=MathF.Sqrt(MathF.Pow(Vector3.Dot(point,surface.Normal)-surface.Height,2)+MathF.Pow(Vector3.Dot(point,edge.Inward)-edge.Offset,2));
            // Once the observed patch advances onto the top, do not pin it
            // back to the edge merely because the torso is still below it.
            if(distance<=band)return(corner,true,distance);
        }
        return([top],false,surface.TopOffset(ContactGeometry.Point(top,pose)).Length());
    }
    // Region-relative rest-skin correspondence; native bone axes, lengths and
    // weights are untouched. This is an inferred patch, not an authored socket.
    public static SkinSurfacePoint[] CorrespondingPatch(SupportRegion source,SupportRegion target,SkinSurfacePoint[] patch,EvaluatedPose sourceRest,EvaluatedPose targetRest,Matrix4x4 sourceBasis,Matrix4x4 targetBasis)
    {
        Vector3 Coordinates(SkinSurfacePoint point,EvaluatedPose rest,Matrix4x4 basis)=>Vector3.TransformNormal(ContactGeometry.Point(point,rest),Matrix4x4.Transpose(basis));
        (Vector3 Min,Vector3 Extent) Bounds(SupportRegion region,EvaluatedPose rest,Matrix4x4 basis)
        {
            var points=region.Points.Select(p=>Coordinates(p,rest,basis)).ToArray();var min=points.Aggregate(Vector3.Min);var max=points.Aggregate(Vector3.Max);return(min,Vector3.Max(max-min,new Vector3(.000001f)));
        }
        var s=Bounds(source,sourceRest,sourceBasis);var t=Bounds(target,targetRest,targetBasis);
        var desired=patch.Select(p=>(Coordinates(p,sourceRest,sourceBasis)-s.Min)/s.Extent).Aggregate(Vector3.Zero,(x,y)=>x+y)/patch.Length;
        return target.Points.OrderBy(p=>Vector3.DistanceSquared((Coordinates(p,targetRest,targetBasis)-t.Min)/t.Extent,desired)).ThenBy(p=>p.Mesh).ThenBy(p=>p.Vertex).Take(1).ToArray();
    }
    public static SkinSurfacePoint[] Clearance(SupportRegion region)
    {
        var points=new HashSet<SkinSurfacePoint>();foreach(int x in new[]{-1,0,1})foreach(int y in new[]{-1,0,1})foreach(int z in new[]{-1,0,1})if(x!=0||y!=0||z!=0)points.Add(region.Points.MaxBy(p=>Vector3.Dot(p.Position,new(x,y,z)))!);return points.ToArray();
    }
    public static GroundContactAnalysis Detect(FighterProject donor,AnimationClip clip,ControlRigSetup rig,GroundContactSettings settings,CancellationToken cancellation=default)
    {
        if(!float.IsFinite(settings.HeightFraction)||settings.HeightFraction<=0||settings.HeightFraction>.2f||settings.LimbHeightFraction is {} limbBand&&(!float.IsFinite(limbBand)||limbBand<=0||limbBand>.2f)||!float.IsFinite(settings.DriftFraction)||settings.DriftFraction<=0||!float.IsFinite(settings.SpeedFractionPerSecond)||settings.SpeedFractionPerSecond<=0||!double.IsFinite(settings.MinimumSeconds)||settings.MinimumSeconds<=0)throw new InvalidOperationException("Ground detection tolerances must be finite, positive and bounded.");
        if(clip.SampleCount>20000)throw new InvalidOperationException("Automatic surface analysis is limited to 20,000 samples per clip; select a shorter source revision or use reviewed contact intervals.");
        var anatomy=RigCharacterization.Describe(donor.Scene,rig);var up=new Vector3(anatomy.Basis.M21,anatomy.Basis.M22,anatomy.Basis.M23);var regions=Regions(donor.Scene,rig);var geometry=new PreparedGeometry(donor,clip);
        float floor=settings.SourceFloor??Floor(donor.Scene,rig,regions,up),height=anatomy.LegLength*settings.HeightFraction,drift=anatomy.LegLength*settings.DriftFraction;
        settings.SourceEdge?.Validate(up);var supportSurface=new SupportSurface(up,floor,settings.SourceEdge);
        if(!float.IsFinite(floor))throw new InvalidOperationException("Ground plane must be finite.");
        int minimum=Math.Max(2,(int)Math.Ceiling(settings.MinimumSeconds*clip.Rate.FramesPerSecond));var contacts=new List<ContactFrame>();
        // Explicit authored intervals, including their releases, take precedence for that region.
        var authored=donor.Contacts.Where(c=>c.Clip==clip.Id).SelectMany(c=>c.Pins).Select(p=>p.Bone).ToHashSet();
        float Band(SupportRegion region)=>anatomy.LegLength*(settings.LimbHeightFraction??(rig.Chains.Any(c=>c.Effector==region.Bone)?DefaultEffectorBandFraction:settings.HeightFraction));
        var active=new Dictionary<int,(int Start,SkinSurfacePoint[] Patch,Vector3 Position,bool Edge,bool Surface,float Motion,Quaternion Orientation,float Angle,HashSet<SkinSurfacePoint> Observed)>();var modes=new List<SupportSurfaceMode>();
        void Finish(SupportRegion region,int end)
        {
            if(!active.Remove(region.Bone,out var entry)||end-entry.Start+1<minimum)return;
            bool cornerGrip=entry.Edge;
            var goal=cornerGrip?entry.Position:entry.Position-supportSurface.TopOffset(entry.Position);
            bool cornerRoll=cornerGrip&&settings.SourceEdge is not null&&entry.Motion>anatomy.LegLength*.002f;
            bool rolling=cornerRoll||entry.Surface&&entry.Motion>anatomy.LegLength*.002f;
            if(cornerRoll)goal=up*floor+settings.SourceEdge!.Inward*settings.SourceEdge.Offset+Vector3.Cross(up,settings.SourceEdge.Inward)*Vector3.Dot(entry.Position,Vector3.Cross(up,settings.SourceEdge.Inward));
            var mode=cornerRoll?ContactMode.Roll:entry.Edge?ContactMode.Grip:rolling?(entry.Angle>3*MathF.PI/180?ContactMode.Roll:ContactMode.Slide):ContactMode.Plant;
            var pin=new ContactPin(Guid.NewGuid(),region.Role,region.Bone,Vector3.Zero,goal,entry.Orientation,cornerGrip&&!cornerRoll&&entry.Angle<=3*MathF.PI/180,entry.Edge?"Detected corner support · surface extent needs review":rolling?"Detected rolling/sliding surface support":"Detected static top support") {SurfacePoints=entry.Patch,SurfaceRegion=cornerRoll?entry.Observed.OrderBy(p=>p.Mesh).ThenBy(p=>p.Vertex).ToArray():rolling?region.Points:[],PlaneNormal=rolling?up:null,EdgeInward=cornerRoll?settings.SourceEdge!.Inward:null,AllowedSurface=!entry.Edge&&settings.SourceEdge is not null?supportSurface:null,Automatic=true,EdgeSupport=entry.Edge,Mode=mode,Provenance=cornerRoll?"Inferred rolling observed-skin contact against supplied ledge corner; lateral travel permitted; classification needs review":cornerGrip?"Inferred persistent skin contact against supplied corner; stable orientation only; classification needs review":"Inferred top-surface observation; experimental classification; not an authored exact world lock"};
            // Classifications belong to sample intervals. Ending at the last
            // integer sample left an unobserved hole before the next sample,
            // including when a corner became a top relationship.
            double finish=end+1;bool endExclusive=true;
            if((clip.Loop||clip.LastKeyFrame>=clip.SampleCount)&&end==clip.SampleCount-1)
            {
                var closing=geometry.Evaluate(clip.SampleCount);var at=entry.Patch.Aggregate(Vector3.Zero,(p,s)=>p+ContactGeometry.Point(s,closing))/entry.Patch.Length;
                if(entry.Surface?region.Points.Min(p=>supportSurface.TopOffset(ContactGeometry.Point(p,closing)).Length())<=Band(region):Vector3.Distance(at,entry.Position)<=drift){finish=clip.SampleCount;endExclusive=false;}
            }
            contacts.Add(new(Guid.NewGuid(),clip.Id,entry.Start,rig.SkeletonFingerprint,[pin]){EndFrame=finish,EndExclusive=endExclusive});
        }
        var torso=regions.Where(r=>r.Role is "Hips" or "Chest").ToArray();var lowBody=new bool[clip.SampleCount];var bodySamples=new bool[clip.SampleCount];
        bool BodyNear(EvaluatedPose pose)=>torso.Length>0&&torso.Average(r=>r.Points.Average(p=>Vector3.Dot(ContactGeometry.Point(p,pose),up)))<=floor+anatomy.LegLength*.55f&&(settings.SourceEdge is not null?torso.SelectMany(r=>r.Points).Min(p=>supportSurface.TopOffset(ContactGeometry.Point(p,pose)).Length())<=anatomy.LegLength*.1f:torso.SelectMany(r=>r.Points).Min(p=>Vector3.Dot(ContactGeometry.Point(p,pose),up))<=floor+anatomy.LegLength*.1f);
        bool edge=false;int clear=0;EvaluatedPose? previous=null;for(int f=0;f<clip.SampleCount;f++)
        {
            cancellation.ThrowIfCancellationRequested();var pose=geometry.Evaluate(f);
            float bodyMinimum=regions.SelectMany(r=>r.Points).Min(p=>Vector3.Dot(ContactGeometry.Point(p,pose),up));
            float coreMinimum=torso.Length>0?torso.SelectMany(r=>r.Points).Min(p=>Vector3.Dot(ContactGeometry.Point(p,pose),up)):bodyMinimum;
            float bodyCentre=torso.Length>0?torso.Average(r=>r.Points.Average(p=>Vector3.Dot(ContactGeometry.Point(p,pose),up))):Vector3.Dot(pose.World[rig.BodyBone??anatomy.Roles.GetValueOrDefault("Hips",rig.Chains[0].Root)].Translation,up);
            if(settings.SurfaceMode==SupportSurfaceMode.Automatic)
            {
                // A single penetrating sole/palm is a ground problem, not evidence of a ledge.
                // Infer a below-edge phase only from the reviewed torso's skinned centre.
                // Frozen context bound: a low torso near the known plane can keep
                // clearance through rolling/body-support changes. It is not a pin.
                lowBody[f]=bodyCentre<=floor+anatomy.LegLength*.55f&&bodyMinimum<=floor+anatomy.LegLength*.1f;
                bool outsidePlatform=settings.SourceEdge is not {} boundary||torso.SelectMany(r=>r.Points).Any(p=>!supportSurface.AboveTop(ContactGeometry.Point(p,pose)));
                if((!settings.ExplicitSurface||settings.SourceEdge is not null)&&outsidePlatform&&bodyCentre<floor-anatomy.LegLength*.08f){edge=true;clear=0;}
                // Enter and leave the below-edge mode using the same core
                // geometry. A penetrating toe or changing hand patch cannot
                // keep an otherwise above-platform body hanging indefinitely.
                else if(edge){clear=coreMinimum>=floor-anatomy.LegLength*.04f?clear+1:0;if(clear>=minimum)edge=false;}
            }
            else edge=settings.SurfaceMode==SupportSurfaceMode.Edge;
            modes.Add(settings.SurfaceMode==SupportSurfaceMode.Free?SupportSurfaceMode.Free:edge?SupportSurfaceMode.Edge:SupportSurfaceMode.Ground);
            // Whole-clip rest/roll context requires actual core skin near the
            // plane, not just a low torso over planted hands/feet (crawl/handstand).
            bodySamples[f]=modes[^1]==SupportSurfaceMode.Ground&&BodyNear(pose);
            foreach(var region in regions.Where(r=>!authored.Contains(r.Bone)))
            {
                float band=Band(region);var observed=SupportPatch(region,pose,supportSurface,band);
                bool cornerContact=observed.Corner;var candidate=observed.Patch;
                bool surface=!cornerContact;
                bool near=observed.Separation<=band&&settings.SurfaceMode!=SupportSurfaceMode.Free;
                bool Slow(SkinSurfacePoint[] points)=>previous is null||points.Average(p=>Vector3.Distance(ContactGeometry.Point(p,pose),ContactGeometry.Point(p,previous)))*clip.Rate.FramesPerSecond<=anatomy.LegLength*settings.SpeedFractionPerSecond;
                // A fast approach/release near a surface is not a plant. For
                // tangential travel allow an explicitly classified slide/roll,
                // retaining the existing temporal persistence requirement.
                // Use the contact locus, not the velocity of a material vertex
                // that can roll away while another vertex acquires support.
                // Persistence and distance are still required, with uncertain
                // slide/roll classification explicitly retained as review.
                bool SlowLocus()
                {
                    if(previous is null)return true;var old=SupportPatch(region,previous,supportSurface,band);
                    // Persistent proximity is stronger evidence than a speed
                    // spike from discretized nearest-point switching. This
                    // retains the relationship; it does not label it sticking.
                    if(old.Separation<=band&&observed.Separation<=band)return true;
                    // Nearest vertices can alternate across the corner with
                    // opposite signed offsets. Their point-to-point jump is
                    // mesh tessellation, not observed approach/release speed.
                    return Math.Abs(observed.Separation-old.Separation)*clip.Rate.FramesPerSecond<=anatomy.LegLength*settings.SpeedFractionPerSecond;
                }
                near&=SlowLocus();
                if(active.TryGetValue(region.Bone,out var entry))
                {
                    if(cornerContact)entry.Observed.UnionWith(candidate);
                    var point=entry.Patch.Aggregate(Vector3.Zero,(p,s)=>p+ContactGeometry.Point(s,pose))/entry.Patch.Length;
                    var movement=point-entry.Position;bool stationary=movement.Length()<=drift&&Slow(entry.Patch);
                    entry.Motion=Math.Max(entry.Motion,movement.Length());active[region.Bone]=entry;
                    entry.Angle=Math.Max(entry.Angle,2*MathF.Acos(Math.Clamp(Math.Abs(Quaternion.Dot(entry.Orientation,PoseGeometry.Rotation(pose.World[region.Bone]))),0,1)));active[region.Bone]=entry;
                    if(!near||(!surface&&!stationary&&!cornerContact)||entry.Edge!=cornerContact||entry.Surface!=surface){Finish(region,f-1);}
                }
                if(near&&(surface||cornerContact||Slow(candidate))&&!active.ContainsKey(region.Bone))
                {
                    var patch=candidate;var point=patch.Aggregate(Vector3.Zero,(p,s)=>p+ContactGeometry.Point(s,pose))/patch.Length;
                    active[region.Bone]=(f,patch,point,cornerContact,surface,0,PoseGeometry.Rotation(pose.World[region.Bone]),0,new(patch));
                }
            }
            previous=pose;
        }
        foreach(var region in regions)Finish(region,clip.SampleCount-1);
        // Model-space floor height is not an actor/world trajectory. With no stable
        // support evidence, preserve the airborne/unknown phase instead of flattening it.
        int inferredClearance=0;
        if(settings.SurfaceMode==SupportSurfaceMode.Automatic)for(int i=0;i<modes.Count;i++)if(modes[i]==SupportSurfaceMode.Ground&&!contacts.Any(c=>c.Contains(i))&&!donor.Contacts.Any(c=>c.Clip==clip.Id&&c.Contains(i))){if(lowBody[i])inferredClearance++;else modes[i]=SupportSurfaceMode.Free;}
        int bracketedClearance=0;
        if(settings.SurfaceMode==SupportSurfaceMode.Automatic)for(int start=0;start<modes.Count;)
        {
            int end=start;while(end+1<modes.Count&&modes[end+1]==modes[start])end++;
            // Preserve the existing plane through a brief gap between two ground
            // observations. Inequality clearance permits jumps; it is never a plant.
            if(modes[start]==SupportSurfaceMode.Free&&start>0&&end+1<modes.Count&&modes[start-1]==SupportSurfaceMode.Ground&&modes[end+1]==SupportSurfaceMode.Ground&&(end-start+1)/clip.Rate.FramesPerSecond<=.12)
                for(int i=start;i<=end;i++){modes[i]=SupportSurfaceMode.Ground;bracketedClearance++;}
            start=end+1;
        }
        var phases=new List<SupportSurfacePhase>();for(int start=0;start<modes.Count;){int end=start;while(end+1<modes.Count&&modes[end+1]==modes[start])end++;phases.Add(new(start,end,modes[start]));start=end+1;}
        var bodyPhases=new List<SupportSurfacePhase>();for(int start=0;start<bodySamples.Length;){int end=start;while(end+1<bodySamples.Length&&bodySamples[end+1]==bodySamples[start])end++;if(bodySamples[start]&&end-start+1>=minimum)bodyPhases.Add(new(start,end,SupportSurfaceMode.Ground));start=end+1;}
        var closingPose=geometry.Evaluate(clip.SampleCount);bool bodyClosing=modes.LastOrDefault()==SupportSurfaceMode.Ground&&BodyNear(closingPose)&&torso.Average(r=>r.Points.Average(p=>Vector3.Dot(ContactGeometry.Point(p,closingPose),up)))>=floor-anatomy.LegLength*.08f;
        var diagnostics=new List<Diagnostic>{new("GROUND_DETECTION",$"Detected {contacts.Count} skinned support intervals ({string.Join(", ",contacts.SelectMany(c=>c.Pins).Select(p=>p.Role).Distinct())}); source plane {floor:G6}. Explicit region annotations take precedence. Moving surfaces and props require reviewed anchors.",false)};
        if(inferredClearance>0)diagnostics.Add(new("INFERRED_CLEARANCE_CONTEXT",$"{inferredClearance} low-body samples retain the known static-plane clearance through changing support. Torso-centre bound: 0.55 leg lengths; nearby geometry bound: 0.1. No contact anchor or seat was invented; review this inferred scene context.",false));
        if(bracketedClearance>0)diagnostics.Add(new("INFERRED_CLEARANCE_CONTEXT",$"{bracketedClearance} samples bridge ground observations across gaps of at most 0.12 seconds. Only nonpenetration is inferred, never a fixed contact; edge phases are excluded. Review this inferred scene context.",false));
        if(phases.Any(p=>p.Mode==SupportSurfaceMode.Free)&&settings.SurfaceMode==SupportSurfaceMode.Automatic)diagnostics.Add(new("UNSUPPORTED_SURFACE_CONTEXT","Samples without a stable detected/authored support retain their model-space motion. Review actor/world trajectory and ground context; absence of a contact is not proof that the mesh clears a game-world floor.",false));
        if(phases.Any(p=>p.Mode==SupportSurfaceMode.Edge))diagnostics.Add(new("EDGE_EXTENT_REVIEW","Below-plane phases: "+string.Join(", ",phases.Where(p=>p.Mode==SupportSurfaceMode.Edge).Select(p=>$"{p.Start:G6}–{p.End:G6}"))+". Preserve hanging/edge motion below the top plane; infinite-floor clearance is disabled there. Review the actual ledge extent and grip mapping. Above-plane phases use ground support independently.",false));
        return new(floor,regions,contacts.ToArray(),diagnostics.ToArray()){Phases=phases.ToArray(),BodySupportedThroughout=bodyPhases.Sum(p=>p.End-p.Start+1)==clip.SampleCount&&clip.SampleCount>0,BodySupportPhases=bodyPhases.ToArray(),BodySupportedAtClosing=bodyClosing};
    }
}
