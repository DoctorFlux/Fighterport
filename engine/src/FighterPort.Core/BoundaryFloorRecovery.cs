using System.Numerics;
using System.Security.Cryptography;
using System.Text;

namespace FighterPort.Core;

public sealed record BoundaryFloorSettings(float[] Normal,float Height,bool Reviewed,string Evidence);
public sealed record BoundaryFloorRegionReceipt(string Role,string Bone,int PointCount,int LegacyPointCount,
    double MinimumSource,double MinimumReference,double MinimumBefore,double MinimumAfter,double MaximumAddedPenetration);
public sealed record BoundaryFloorReceipt(string Version,string GeometryHash,string BaselinePolicy,BoundaryFloorSettings Plane,
    double MaximumLift,double MaximumAddedPenetration,int Evaluations,int ValidationSamples,int KnotCount,BoundaryFloorRegionReceipt[] Regions)
{
    public double LiftLimit {get;init;}
    public double AllowedAddedPenetration {get;init;}
    public string RegionPolicy {get;init;}="all vertices with any positive influence on each reviewed ankle and descendants; all original weight slots retained";
    public string Coverage {get;init;}="integer native knots; 1/32-frame fitting, 1/64-frame and .37/.73 held-out validation; no continuous-time certificate";
    public string EnvelopePolicy {get;init;}="conservative piecewise native Hermite plateau; zero correction and slope at join/end, unit interior integer knots; not a minimum-lift or planted-contact solution";
}
internal sealed record BoundaryFloorResult(AnimationClip Clip,BoneTrack[] Changed,BoundaryFloorReceipt Receipt);

// An explicitly reviewed static-plane observation, not contact inference or IK.
// One scalar clearance envelope changes only the existing body's translation.
// Binds, weights, skeleton, leg rotations and source-authored roots are immutable.
public static class BoundaryFloorRecovery
{
    public const string BaselinePolicy="per-region min(0, original, authentic reference body under source-root placement)";
    public const string ReferenceControlBaselinePolicy="per-region min(0, original, authentic reference under retained motion-position control)";
    public const double PositionBound=.0002;
    const int KeyLimit=8192,EvaluationLimit=200000;
    const double LiftFraction=.20,DerivativeStep=1d/1024;
    sealed record Region(string Role,int Bone,SkinSurfacePoint[] Points,int LegacyPoints);
    sealed class Measurements
    {
        public double Source=double.PositiveInfinity,Reference=double.PositiveInfinity,Before=double.PositiveInfinity,After=double.PositiveInfinity,Added;
    }

    internal static long PlannedEvaluations(int span)=>4*(98L*span+2)+3L*Math.Max(0,span-1)+17;

    internal static BoundaryFloorResult Prepare(FighterProject project,AnimationClip original,AnimationClip candidate,BoundaryAnchor reference,
        BoundaryFitSettings settings,ControlRigSetup controls,ISet<string> protectedMotion,int join,int? windowEnd=null)
    {
        var plane=settings.FloorSettings!;
        if(!plane.Reviewed||string.IsNullOrWhiteSpace(plane.Evidence)||plane.Evidence.Length>2048||plane.Normal is not {Length:3}||
            plane.Normal.Any(x=>!float.IsFinite(x))||!float.IsFinite(plane.Height))
            throw new InvalidDataException("Floor recovery requires an explicit reviewed finite support plane and its evidence.");
        var normal=new Vector3(plane.Normal[0],plane.Normal[1],plane.Normal[2]);
        if(Math.Abs(normal.LengthSquared()-1)>.0001)
            throw new InvalidDataException("The reviewed floor normal must already be normalized; no implicit plane conversion is permitted.");
        var scene=project.Scene;scene.Validate();int body=AnimationControls.ResolveBodyBone(scene,controls),last=windowEnd??original.SampleCount-1,span=last-join;
        if(body<0||!settings.Bones.Contains(scene.Bones[body].Name,StringComparer.Ordinal)||protectedMotion.Contains(scene.Bones[body].Name))
            throw new InvalidDataException("Floor recovery requires the reviewed editable body in the complete recovery mask.");
        if(3L*(span+1)>KeyLimit)throw new NotSupportedException("Floor recovery exceeds the 8192 integral body-key budget.");
        // No point is sampled/capped out. Oversized work is rejected as a whole.
        long fitWitnesses=32L*span+1,validationWitnesses=66L*span+1;
        long plannedEvaluations=4*(fitWitnesses+validationWitnesses)+3L*Math.Max(0,span-1)+1;
        if(plannedEvaluations+16>EvaluationLimit)
            throw new NotSupportedException("Floor recovery exceeds the 200000 pose-evaluation budget.");
        var legs=controls.Chains.Where(c=>c.Reviewed&&c.Role is "Foot.L" or "Foot.R").OrderBy(c=>c.Role,StringComparer.Ordinal).ToArray();
        var regions=Regions(scene,legs);
        if(regions.Length==0||regions.Any(r=>r.Points.Length==0))throw new NotSupportedException("Every reviewed foot region requires authentic weighted mesh geometry; no bone-only replacement is inferred.");
        long points=regions.Sum(r=>(long)r.Points.Length);
        if(points*4*(fitWitnesses+validationWitnesses)>200000000)
            throw new NotSupportedException("The complete uncapped foot geometry exceeds the bounded recovery work budget; no vertices were discarded.");
        int evaluations=0;
        EvaluatedPose Evaluate(AnimationClip? clip,double frame,IReadOnlyDictionary<int,Transform>? overrides=null)
        {
            if(++evaluations>EvaluationLimit)throw new NotSupportedException("Floor recovery exceeded its pose-evaluation budget without publishing a result.");
            var pose=AnimationService.Evaluate(scene,clip,frame,overrides);
            if(pose.Local.Any(p=>p.Channels().Any(v=>!float.IsFinite(v)))||pose.World.Any(m=>!Finite(m)))
                throw new InvalidDataException("Floor recovery encountered nonfinite native transforms.");
            return pose;
        }
        var bind=Evaluate(null,0);
        double liftLimit=LiftFraction*legs.Min(c=>(double)Vector3.Distance(bind.World[c.Root].Translation,bind.World[c.Middle].Translation)+Vector3.Distance(bind.World[c.Middle].Translation,bind.World[c.Effector].Translation));
        if(!double.IsFinite(liftLimit)||liftLimit<=0)throw new NotSupportedException("Reviewed leg geometry has no finite nonzero clearance budget.");
        var referenceOverrides=Enumerable.Range(0,scene.Bones.Length).Where(i=>!protectedMotion.Contains(scene.Bones[i].Name))
            .ToDictionary(i=>i,i=>reference.Clip.Sample(scene.Bones[i],reference.Frame));
        int parent=scene.Bones[body].Parent;
        Vector3 LocalNormal(double frame)
        {
            var pose=Evaluate(candidate,frame);var matrix=parent<0?Matrix4x4.Identity:pose.World[parent];
            if(!Matrix4x4.Invert(matrix,out var inverse)||!Finite(inverse))
                throw new NotSupportedException("A singular protected parent transform contradicts body-normal floor recovery; source roots are retained.");
            var local=Vector3.TransformNormal(normal,inverse);
            if(!PoseGeometry.Finite(local))throw new InvalidDataException("The reviewed normal has no finite native body-local direction.");
            return local;
        }
        var offsets=new Vector3[span+1];var slopes=new Vector3[span+1];
        for(int step=1;step<span;step++)
        {
            double at=join+step;
            var direction=LocalNormal(at);
            var derivative=(LocalNormal(at+DerivativeStep)-LocalNormal(at-DerivativeStep))/(float)(2*DerivativeStep);
            // A local one-interval C1 rise/return avoids forcing the curvature
            // at the join to scale with the square of the entire exit duration.
            // Interior knots share a conservative normal plateau. Parent motion
            // is retained and only its inverse direction is differentiated.
            offsets[step]=direction;slopes[step]=derivative;
        }
        var hip=candidate.Tracks.Single(t=>t.Bone==scene.Bones[body].Name);
        AnimationClip Lift(double amplitude)
        {
            if(amplitude==0)return candidate;
            var channels=hip.Channels.ToArray();
            for(int axis=0;axis<3;axis++)
            {
                int channel=axis+6;var curve=channels[channel]??throw new InvalidDataException("Body recovery has no fitted translation channel.");
                var keys=curve.Keys.Where(k=>k.Frame<=join).ToList();
                for(int step=1;step<span;step++)
                {
                    int frame=join+step;float value=curve.Sample(frame)+(float)(amplitude*offsets[step][axis]);
                    float incoming=BoundaryFitting.Derivative(curve,frame,false)+(float)(amplitude*slopes[step][axis]);
                    float outgoing=BoundaryFitting.Derivative(curve,frame)+(float)(amplitude*slopes[step][axis]);
                    if(!float.IsFinite(value)||!float.IsFinite(incoming)||!float.IsFinite(outgoing))throw new InvalidDataException("The clearance envelope produced a nonfinite body key.");
                    keys.Add(new(frame,value,incoming,outgoing));
                }
                keys.AddRange(curve.Keys.Where(k=>k.Frame>=last));
                channels[channel]=curve with{Keys=keys.ToArray()};
            }
            var lifted=hip with{Channels=channels};return candidate with{Tracks=candidate.Tracks.Select(t=>ReferenceEquals(t,hip)?lifted:t).ToArray()};
        }
        var unit=Lift(1);
        var fitTimes=Times(join,last,32,false);var validationTimes=Times(join,last,64,true);
        double amplitude=0,maximumUnitLift=0,maximumPointwiseLift=0,limitingFrame=join;string limitingRole="";
        // The response is measured from the actual integral-key Hermite curve,
        // not an ideal continuous field. All inequalities share this scalar A.
        void Require(double at,bool update)
        {
            var source=Evaluate(original,at);var pose=Evaluate(candidate,at);var one=Evaluate(unit,at);
            var anchor=Evaluate(original,at,referenceOverrides);
            maximumUnitLift=Math.Max(maximumUnitLift,Vector3.Distance(pose.World[body].Translation,one.World[body].Translation));
            foreach(var region in regions)
            {
                double baseline=Math.Min(0,Math.Min(Minimum(region,source),Minimum(region,anchor)));
                foreach(var point in region.Points)
                {
                    double before=Height(point,pose),deficit=baseline-before;
                    if(deficit<=0)continue;
                    double bodyWeight=0;for(int slot=0;slot<point.InfluenceCount;slot++)if(point.WeightAt(slot)>0&&PoseGeometry.Ancestor(scene,body,(int)point.JointAt(slot)))bodyWeight+=point.WeightAt(slot);
                    if(bodyWeight>0)maximumPointwiseLift=Math.Max(maximumPointwiseLift,deficit/bodyWeight);
                    double response=Height(point,one)-before;
                    if(response<=1e-10)
                    {
                        if(deficit>PositionBound)throw new NotSupportedException("The integral body-normal envelope cannot resolve this foot clearance inequality without changing roots or endpoint constraints.");
                        continue;
                    }
                    if(update&&deficit/response>amplitude){amplitude=deficit/response;limitingFrame=at;limitingRole=region.Role;}
                }
            }
        }
        double Height(SkinSurfacePoint point,EvaluatedPose pose)
        {
            var p=ContactGeometry.Point(point,pose);if(!PoseGeometry.Finite(p))throw new InvalidDataException("A weighted foot point evaluated to nonfinite geometry.");
            return Vector3.Dot(p,normal)-plane.Height;
        }
        double Minimum(Region region,EvaluatedPose pose)
        {double minimum=double.PositiveInfinity;foreach(var point in region.Points)minimum=Math.Min(minimum,Height(point,pose));return minimum;}
        foreach(double at in fitTimes)Require(at,true);
        // Fixed numeric padding is one eighth of the position gate. It vanishes
        // with the envelope at both ends; it is not a per-rig tuning parameter.
        if(amplitude>0)amplitude+=PositionBound/8;
        if(amplitude*maximumUnitLift>liftLimit)throw new NotSupportedException(FormattableString.Invariant($"Floor recovery would exceed 20% of the shorter reviewed leg chain: envelope lift {amplitude*maximumUnitLift:G9}, pointwise required lift {maximumPointwiseLift:G9}, limit {liftLimit:G9}, limiting {limitingRole} at native key time {limitingFrame:G9}. Source roots and the authentic endpoint remain unchanged."));
        var output=Lift(amplitude);
        Measurements[] Measure(AnimationClip clip,double[] times,out double added,out double lift)
        {
            var values=regions.Select(_=>new Measurements()).ToArray();added=lift=0;
            foreach(double at in times)
            {
                var source=Evaluate(original,at);var before=Evaluate(candidate,at);var after=Evaluate(clip,at);var anchor=Evaluate(original,at,referenceOverrides);
                lift=Math.Max(lift,Vector3.Distance(before.World[body].Translation,after.World[body].Translation));
                for(int r=0;r<regions.Length;r++)
                {
                    var m=values[r];double s=Minimum(regions[r],source),a=Minimum(regions[r],anchor),b=Minimum(regions[r],before),c=Minimum(regions[r],after);
                    double penetration=Math.Max(0,Math.Min(0,Math.Min(s,a))-c);
                    m.Source=Math.Min(m.Source,s);m.Reference=Math.Min(m.Reference,a);m.Before=Math.Min(m.Before,b);m.After=Math.Min(m.After,c);m.Added=Math.Max(m.Added,penetration);added=Math.Max(added,penetration);
                }
            }
            return values;
        }
        var measurements=Measure(output,validationTimes,out double maximumAdded,out double maximumLift);
        if(maximumAdded>PositionBound)
            throw new NotSupportedException("Floor recovery did not converge at independent 1/64-frame/.37/.73 witnesses within the integral-key envelope. No result was published.");
        if(maximumLift>liftLimit)throw new NotSupportedException("The independently sampled floor correction exceeds the reviewed leg-length lift budget.");
        var receipt=new BoundaryFloorReceipt(settings.BoundaryContactRecovery?BoundaryFitting.BoundaryFloorVersion:BoundaryFitting.FloorVersion,GeometryHash(scene,legs),settings.MatchReferenceControls?ReferenceControlBaselinePolicy:BaselinePolicy,
            plane with{Normal=plane.Normal.ToArray()},maximumLift,maximumAdded,evaluations,validationTimes.Length,amplitude==0?0:3*(span+1),
            regions.Select((r,i)=>new BoundaryFloorRegionReceipt(r.Role,scene.Bones[r.Bone].Name,r.Points.Length,r.LegacyPoints,
                measurements[i].Source,measurements[i].Reference,measurements[i].Before,measurements[i].After,measurements[i].Added)).ToArray())
            {LiftLimit=liftLimit,AllowedAddedPenetration=PositionBound};
        return new(output,amplitude==0?[]:[output.Tracks.Single(t=>t.Bone==hip.Bone)],receipt);
    }

    static double[] Times(int first,int last,int subdivisions,bool heldOut)
    {
        var times=new SortedSet<double>();for(int frame=first;frame<last;frame++)
        {
            for(int step=0;step<subdivisions;step++)times.Add(frame+step/(double)subdivisions);
            if(heldOut){times.Add(frame+.37);times.Add(frame+.73);}
        }
        times.Add(last);return times.ToArray();
    }
    static Region[] Regions(Scene scene,ControlChain[] legs)
    {
        return legs.Select(leg=>
        {
            var points=new List<SkinSurfacePoint>();int legacy=0;
            for(int mesh=0;mesh<scene.Meshes.Length;mesh++)for(int vertex=0;vertex<scene.Meshes[mesh].Vertices.Length;vertex++)
            {
                var v=scene.Meshes[mesh].Vertices[vertex];double weight=0;
                for(int slot=0;slot<v.InfluenceCount;slot++)if(v.WeightAt(slot)>0&&PoseGeometry.Ancestor(scene,leg.Effector,(int)v.JointAt(slot)))weight+=v.WeightAt(slot);
                if(weight<=0)continue;
                var point=new SkinSurfacePoint(v.Position,v.Joints,v.Weights){Influences=v.Influences,Mesh=mesh,Vertex=vertex};
                if(!ContactGeometry.Valid(point,scene.Bones.Length))throw new InvalidDataException("A complete foot region contains invalid authentic skin data.");
                points.Add(point);if(weight>=.8)legacy++;
            }
            return new Region(leg.Role,leg.Effector,points.ToArray(),legacy);
        }).ToArray();
    }
    static string GeometryHash(Scene scene,ControlChain[] legs)
    {
        using var hash=IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        void Integer(int value)=>hash.AppendData(BitConverter.GetBytes(value));
        void Float(float value)=>hash.AppendData(BitConverter.GetBytes(value));
        void Text(string text){var bytes=Encoding.UTF8.GetBytes(text);Integer(bytes.Length);hash.AppendData(bytes);}
        Text("weighted-exit-foot-geometry-1");Integer(scene.Bones.Length);
        foreach(var bone in scene.Bones){Text(bone.Name);Integer(bone.Parent);foreach(float v in bone.Rest.Channels())Float(v);}
        Integer(legs.Length);foreach(var leg in legs){Text(leg.Role);Integer(leg.Root);Integer(leg.Middle);Integer(leg.Effector);}
        Integer(scene.Meshes.Length);foreach(var mesh in scene.Meshes)
        {
            Text(mesh.Name);Integer(mesh.Vertices.Length);foreach(var vertex in mesh.Vertices)
            {
                Float(vertex.Position.X);Float(vertex.Position.Y);Float(vertex.Position.Z);
                if(vertex.Influences.Length>0){Integer(int.MinValue);Integer(vertex.InfluenceCount);}
                for(int slot=0;slot<vertex.InfluenceCount;slot++){Float(vertex.JointAt(slot));Float(vertex.WeightAt(slot));}
            }
        }
        return Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant();
    }
    static bool Finite(Matrix4x4 m)=>new[]{m.M11,m.M12,m.M13,m.M14,m.M21,m.M22,m.M23,m.M24,m.M31,m.M32,m.M33,m.M34,m.M41,m.M42,m.M43,m.M44}.All(float.IsFinite);
}
