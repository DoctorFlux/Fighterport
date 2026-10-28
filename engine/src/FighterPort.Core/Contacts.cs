using System.Numerics;

namespace FighterPort.Core;

public enum EditorPinBehavior { Fixed,SoftReach }

// A contact is an editor constraint on an existing bone or calibrated surface point.
// Anchors may describe a floor, ledge, seat, prop or moving reference, in model world space.
public record ContactPin(Guid Id,string Role,int Bone,Vector3 LocalPoint,Vector3 Position,Quaternion Rotation,
    bool HoldRotation,string Surface,Vector3? Pole=null)
{
    public SkinSurfacePoint[] SurfacePoints {get;init;}=[];
    public SkinSurfacePoint[] SurfaceRegion {get;init;}=[];
    public bool Automatic {get;init;}
    public bool EdgeSupport {get;init;}
    public bool SoftSupport {get;init;}
    [System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingDefault)]
    public EditorPinBehavior EditorBehavior {get;init;}
    [System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public EditorPinMotion? EditorMotion {get;init;}
    public Vector3? PlaneNormal {get;init;}
    [System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public Vector3? EdgeInward {get;init;}
    // Allowed support surface in the reference frame (model space when no
    // reference exists). A clear point outside a platform is not planted on it.
    [System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public SupportSurface? AllowedSurface {get;init;}
    public ContactClearance? Clearance {get;init;}
    public ContactMode Mode {get;init;}=ContactMode.Plant;
    public string Provenance {get;init;}="Authored exact pin";
    public ContactReference? Reference {get;init;}
    public string? Socket {get;init;}
    public bool RequiredSupport {get;init;}
    // Transient collision constraint, never persisted as an inferred contact.
    internal bool ClearanceOnly {get;init;}
}
public record ContactFrame(Guid Id,Guid Clip,double Frame,string SkeletonFingerprint,ContactPin[] Pins)
{
    public double? EndFrame {get;init;}
    public bool EndExclusive {get;init;}
    public bool Contains(double frame)=>frame>=Frame&&(EndExclusive?frame<(EndFrame??Frame):frame<=(EndFrame??Frame)+.000001);
}
public record ContactSolution(Dictionary<int,Transform> Transforms,float PositionError,float RotationError,int Iterations);

public static class PoseGeometry
{
    public static bool Finite(Vector3 v)=>float.IsFinite(v.X)&&float.IsFinite(v.Y)&&float.IsFinite(v.Z);
    public static Matrix4x4 RotationMatrix(Matrix4x4 matrix)
    {
        // Extract orientation for constraints only; native transforms are never normalized or rewritten.
        var x=Vector3.Normalize(new(matrix.M11,matrix.M12,matrix.M13));
        var y=new Vector3(matrix.M21,matrix.M22,matrix.M23);y=Vector3.Normalize(y-x*Vector3.Dot(x,y));
        var z=Vector3.Normalize(Vector3.Cross(x,y));
        return new(x.X,x.Y,x.Z,0,y.X,y.Y,y.Z,0,z.X,z.Y,z.Z,0,0,0,0,1);
    }
    public static Quaternion Rotation(Matrix4x4 matrix)=>Quaternion.Normalize(Quaternion.CreateFromRotationMatrix(RotationMatrix(matrix)));
    public static int[] BodyAdjustmentBones(ControlRigSetup rig)=>rig.BoneRoles.Where(r=>r.SemanticRole is "Hips" or "Spine" or "Chest" or "Neck" or "Head").Select(r=>r.Bone).Concat(rig.BodyBone is {} body?[body]:Array.Empty<int>()).Distinct().ToArray();
    // During supported body fitting, judge limbs in their torso/pelvis frame.
    // A deliberate waist turn should carry an arm, not counter-rotate it in world space.
    public static Quaternion FittingRotation(Scene scene,EvaluatedPose pose,int bone,int[] bodyBones,bool bodyFitting)
    {
        if(bodyFitting&&!bodyBones.Contains(bone))for(int p=scene.Bones[bone].Parent;p>=0;p=scene.Bones[p].Parent)
            if(bodyBones.Contains(p))return Rotation(RotationMatrix(pose.World[bone])*Matrix4x4.Transpose(RotationMatrix(pose.World[p])));
        return Rotation(pose.World[bone]);
    }
    public static float RotationDifference(Quaternion a,Quaternion b)=>2*MathF.Acos(Math.Clamp(Math.Abs(Quaternion.Dot(a,b)),0,1))*180/MathF.PI;
    public static Transform WorldRotation(Scene scene,EvaluatedPose pose,int bone,Quaternion desired)
    {
        int parent=scene.Bones[bone].Parent;var parentRotation=parent<0?Matrix4x4.Identity:RotationMatrix(pose.World[parent]);
        return pose.Local[bone] with{Rotation=AnimationControls.Euler(Matrix4x4.CreateFromQuaternion(desired)*Matrix4x4.Transpose(parentRotation),pose.Local[bone].Rotation)};
    }
    public static bool Ancestor(Scene scene,int ancestor,int bone){for(int i=bone;i>=0;i=scene.Bones[i].Parent)if(i==ancestor)return true;return false;}
    public static float Scale(Scene scene,ControlRigSetup setup)
    {
        var lengths=setup.Chains.Where(c=>c.Calibration is not null).Select(c=>c.Calibration!.UpperLength+c.Calibration.LowerLength).Order().ToArray();
        return lengths.Length>0?Math.Max(lengths[lengths.Length/2],.0001f):1;
    }
}

public static class ContactSolver
{
    public const string Version="existing-bone-head-clearance-body-dls-12";
    internal static bool AdaptableBody(ContactPin[] pins)=>pins.Any(p=>!p.ClearanceOnly)&&pins.Where(p=>!p.ClearanceOnly).All(p=>p.Automatic&&p.SoftSupport&&!p.RequiredSupport)&&pins.Any(p=>p.Clearance is not null);
    internal static ContactPin[] WithClearance(ContactPin[] pins,ContactClearance? plane,int body)
        =>plane is null||pins.Any(p=>p.Clearance is not null)?pins:pins.Append(new ContactPin(Guid.Empty,"Clearance",body,Vector3.Zero,Vector3.Zero,Quaternion.Identity,false,"Ground clearance"){Clearance=plane,SoftSupport=true,ClearanceOnly=true}).ToArray();
    internal static float PositionError(ContactPin[] pins,EvaluatedPose pose)
    {
        float error=pins.Select(p=>Math.Max(p.SoftSupport?0:Math.Max(ContactGeometry.Residual(p,pose).Length(),ContactGeometry.AllowedSurfaceResidual(p,pose).Length()),ContactGeometry.Penetration(p,pose))).DefaultIfEmpty().Max();
        var supports=pins.Where(p=>!p.ClearanceOnly).ToArray();
        // An adaptable body-support set still needs one actual point on its
        // support. Collision clearance alone must not let every patch float.
        if(AdaptableBody(pins))error=Math.Max(error,pins.Select(p=>p.Clearance).OfType<ContactClearance>().Max(f=>Math.Abs(ContactGeometry.SupportHeight(f,pose))));
        else if(supports.Length>0&&supports.All(p=>p.SoftSupport))error=Math.Max(error,supports.Min(p=>ContactGeometry.Residual(p,pose).Length()));
        return error;
    }
    public static ContactPin Capture(Scene scene,EvaluatedPose pose,ControlRigSetup setup,string role,string surface="Current support")
    {
        var chain=setup.Chains.Single(c=>c.Role==role);
        return new(Guid.NewGuid(),role,chain.Effector,Vector3.Zero,pose.World[chain.Effector].Translation,PoseGeometry.Rotation(pose.World[chain.Effector]),true,surface,AnimationControls.DefaultPole(scene,pose,chain));
    }
    public static int BodyBone(Scene scene,ControlRigSetup setup)
        =>AnimationControls.ResolveBodyBone(scene,setup);
    public static OperationResult<ContactSolution> Solve(Scene scene,EvaluatedPose input,ControlRigSetup setup,
        IReadOnlyDictionary<int,Transform> edits,ContactPin[] pins,bool adjustBody=false,int iterations=48,bool adaptAutomatic=false,bool broadJoints=false,EvaluatedPose? referencePose=null)
    {
        OperationResult<ContactSolution> Fail(string code,string message)=>new(null,[new(code,message)],[]);
        if(setup.SkeletonFingerprint!=AnimationControls.Fingerprint(scene))return Fail("RIG_CHANGED","Contacts belong to a different native skeleton/rest revision.");
        if(pins.Length==0)return new(new(edits.ToDictionary(),0,0,0),[],[]);
        if(pins.Any(p=>p.Bone<0||p.Bone>=scene.Bones.Length||!PoseGeometry.Finite(p.Position)||!PoseGeometry.Finite(p.LocalPoint)||!float.IsFinite(p.Rotation.LengthSquared())||p.Rotation.LengthSquared()<.0001f||!ContactGeometry.Valid(p,scene.Bones.Length)))return Fail("INVALID_CONTACT","Contact bone, skin samples, plane, point and orientation must be valid and finite.");
        if(edits.Any(e=>e.Key<0||e.Key>=scene.Bones.Length||e.Value.Channels().Any(v=>!float.IsFinite(v))))return Fail("INVALID_EDIT","Body/FK edits must be finite existing-bone transforms.");
        for(int i=0;i<input.Local.Length;i++)if(input.Local[i].Scale.X<=0||input.Local[i].Scale.Y<=0||input.Local[i].Scale.Z<=0)return Fail("CONTACT_SCALE","Nonpositive scale is not supported by this contact solver; native channels were retained.");
        float unit=PoseGeometry.Scale(scene,setup),tolerance=unit*.0001f;
        if(pins.Any(p=>p.Clearance is not null)){var lengths=setup.Chains.Where(c=>c.Role.StartsWith("Foot.",StringComparison.Ordinal)&&c.Calibration is not null).Select(c=>c.Calibration!.UpperLength+c.Calibration.LowerLength).ToArray();if(lengths.Length>0)tolerance=Math.Min(tolerance,lengths.Average()*.0001f);}
        if(referencePose is not null)tolerance*=.5f; // Retarget-key margin for native interpolation; acceptance is unchanged.
        EvaluatedPose EvaluateLocal(IReadOnlyDictionary<int,Transform> transforms)=>AnimationService.Evaluate(scene,null,0,transforms) with{Seconds=input.Seconds,GeometryPositions=input.GeometryPositions};
        var local=input.Local.Select((t,i)=>(t,i)).ToDictionary(x=>x.i,x=>x.t);foreach(var edit in edits)local[edit.Key]=edit.Value;
        var pose=EvaluateLocal(local);
        // A direct chain with its parent fixed by an authored FK edit has a real
        // reach bound. This is distinct from DLS merely failing to converge.
        int fixedBody=BodyBone(scene,setup);var impossible=new List<string>();
        foreach(var pin in pins.Where(p=>p.Reference is null&&p.SurfacePoints.Length==0&&p.SurfaceRegion.Length==0))
        {
            var chain=setup.Chains.FirstOrDefault(c=>c.Effector==pin.Bone);if(chain is null||scene.Bones[chain.Root].Parent!=fixedBody||!edits.ContainsKey(fixedBody)||input.Local.Any(t=>Math.Abs(t.Scale.X-t.Scale.Y)>.00001f||Math.Abs(t.Scale.Y-t.Scale.Z)>.00001f))continue;
            var origin=pose.World[chain.Root].Translation;float reach=Vector3.Distance(origin,pose.World[chain.Middle].Translation)+Vector3.Distance(pose.World[chain.Middle].Translation,pose.World[chain.Effector].Translation)+Vector3.TransformNormal(pin.LocalPoint,pose.World[pin.Bone]).Length();
            float needed=pin.PlaneNormal is {} plane?Math.Abs(Vector3.Dot(pin.Position-origin,plane)):Vector3.Distance(pin.Position,origin);
            if(needed>reach+tolerance)impossible.Add($"{pin.Role} [{pin.Id}]: required distance {needed:G6}, maximum {reach:G6}");
        }
        if(impossible.Count>0)return new(null,[new("CONTACT_CONFLICT","Authored fixed-body reach bounds conflict with contacts: "+string.Join("; ",impossible)),new("PROVEN_INCOMPATIBLE","Direct fixed-parent reach bound exceeded; no stretching, release or source edit was applied.")],[]);
        var supports=pins.Where(p=>!p.ClearanceOnly).ToArray();
        bool adaptable=adaptAutomatic&&AdaptableBody(pins);
        if(adaptAutomatic&&!adaptable&&supports.Length>0&&supports.All(p=>p.SoftSupport))
        {
            var primary=supports.MinBy(p=>Vector3.Dot(ContactGeometry.Point(p,pose),p.PlaneNormal??Vector3.UnitY))!;
            pins=pins.Select(p=>p.Id==primary.Id?p with{SoftSupport=false}:p).ToArray();
        }
        // An inferred body support is not an exact simultaneous lock on every
        // observed patch. Start by translating the existing body, preserving its
        // transferred articulation, rather than folding the torso to clear skin.
        int seedBody=BodyBone(scene,setup);
        if(referencePose is not null&&adjustBody&&seedBody>=0&&!edits.ContainsKey(seedBody)&&supports.All(p=>p.Automatic&&!p.EdgeSupport)&&pins.Select(p=>p.Clearance).OfType<ContactClearance>().FirstOrDefault() is {} seedFloor)
        {
            float depth=seedFloor.Points.Select(p=>seedFloor.Height-Vector3.Dot(ContactGeometry.Point(p,pose),seedFloor.Normal)).DefaultIfEmpty().Max();
            if(depth>0)
            {
                var shift=seedFloor.Normal*depth;int parent=scene.Bones[seedBody].Parent;
                if(parent>=0&&Matrix4x4.Invert(pose.World[parent],out var inverse))shift=Vector3.TransformNormal(shift,inverse);
                local[seedBody]=local[seedBody] with{Translation=local[seedBody].Translation+shift};pose=EvaluateLocal(local);
            }
        }
        if(referencePose is not null&&adjustBody&&adaptable&&seedBody>=0&&!edits.ContainsKey(seedBody)&&pins.Select(p=>p.Clearance).OfType<ContactClearance>().FirstOrDefault(c=>c.SupportPoints.Length>0) is {} bodyFloor)
        {
            // Start a body-rest solve on the torso, then clear an oversized head
            // with existing distal joints before asking a global DLS solve to
            // move everything. This deterministic geometric seed avoids a cheap
            // whole-body tilt choosing a different limb branch on each frame.
            void SupportTorso()
            {
                var shift=-bodyFloor.Normal*ContactGeometry.SupportHeight(bodyFloor,pose);int parent=scene.Bones[seedBody].Parent;
                if(parent>=0&&Matrix4x4.Invert(pose.World[parent],out var inverse))shift=Vector3.TransformNormal(shift,inverse);
                local[seedBody]=local[seedBody] with{Translation=local[seedBody].Translation+shift};pose=EvaluateLocal(local);
            }
            double DepthCost(EvaluatedPose value)=>bodyFloor.Points.Sum(p=>Math.Pow(Math.Min(0,Vector3.Dot(ContactGeometry.Point(p,value),bodyFloor.Normal)-bodyFloor.Height)/unit,2));
            if(Math.Abs(ContactGeometry.SupportHeight(bodyFloor,pose))>unit*.002f||bodyFloor.Points.Any(p=>Vector3.Dot(ContactGeometry.Point(p,pose),bodyFloor.Normal)<bodyFloor.Height-unit*.002f))
            {
                SupportTorso();
                foreach(int bone in setup.BoneRoles.Where(r=>r.SemanticRole is "Head" or "Neck").OrderBy(r=>r.SemanticRole=="Head"?0:1).Select(r=>r.Bone).Distinct().Where(b=>!edits.ContainsKey(b)&&!setup.PreservedHelperBones.Contains(b)))
                {
                    var head=bodyFloor.Points.Where(p=>Enumerable.Range(0,p.InfluenceCount).Any(j=>p.WeightAt(j)>0&&PoseGeometry.Ancestor(scene,bone,(int)p.JointAt(j)))).ToArray();
                    if(head.Length==0||head.All(p=>Vector3.Dot(ContactGeometry.Point(p,pose),bodyFloor.Normal)>=bodyFloor.Height-tolerance))continue;
                    var centre=head.Aggregate(Vector3.Zero,(s,p)=>s+ContactGeometry.Point(p,pose))/head.Length;
                    var axis=Vector3.Cross(centre-pose.World[bone].Translation,bodyFloor.Normal);if(axis.LengthSquared()<1e-10f)axis=setup.Axes?.Right??Vector3.UnitX;axis=Vector3.Normalize(axis);
                    var origin=local.ToDictionary();var rotation=PoseGeometry.Rotation(pose.World[bone]);var best=origin;double bestCost=DepthCost(pose);
                    foreach(int degrees in new[]{-90,-75,-60,-45,-30,-15,15,30,45,60,75,90})
                    {
                        local=origin.ToDictionary();pose=EvaluateLocal(local);
                        local[bone]=PoseGeometry.WorldRotation(scene,pose,bone,Quaternion.Concatenate(rotation,Quaternion.CreateFromAxisAngle(axis,degrees*MathF.PI/180)));pose=EvaluateLocal(local);SupportTorso();
                        double cost=DepthCost(pose)+1e-6*Math.Pow(degrees*Math.PI/180,2);if(cost<bestCost){bestCost=cost;best=local.ToDictionary();}
                    }
                    local=best;pose=EvaluateLocal(local);
                }
            }
        }
        // Direct, uniform chains get a deterministic analytic seed and their calibrated bend plane.
        foreach(var pin in pins)
        {
            var chain=setup.Chains.FirstOrDefault(c=>c.Effector==pin.Bone);
            if(chain is null||adaptAutomatic&&pin.SoftSupport||edits.ContainsKey(chain.Root)||edits.ContainsKey(chain.Middle))continue;
            var point=ContactGeometry.Point(pin,pose);var goal=ContactGeometry.Normal(pin,pose) is {} normal?point-normal*Vector3.Dot(point-ContactGeometry.Target(pin,pose),normal):ContactGeometry.Target(pin,pose);
            var orientation=PoseGeometry.Rotation(pose.World[pin.Bone]);var originGoal=goal-(point-pose.World[pin.Bone].Translation);
            var pole=pin.Pole??(referencePose is null?AnimationControls.DefaultPole(scene,pose,chain):pose.World[chain.Root].Translation+AnimationControls.DefaultPole(scene,referencePose,chain)-referencePose.World[chain.Root].Translation);
            var seed=AnimationControls.Solve(scene,pose,setup,chain,originGoal,pole);
            if(seed.Value is not null){foreach(var value in seed.Value.Transforms)local[value.Key]=value.Value;pose=EvaluateLocal(local);}
            if(pin.SurfaceRegion.Length>0&&!edits.ContainsKey(pin.Bone)){local[pin.Bone]=PoseGeometry.WorldRotation(scene,pose,pin.Bone,orientation);pose=EvaluateLocal(local);}
            if(pin.HoldRotation&&!edits.ContainsKey(pin.Bone)){local[pin.Bone]=PoseGeometry.WorldRotation(scene,pose,pin.Bone,ContactGeometry.TargetRotation(pin,pose));pose=EvaluateLocal(local);}
        }
        int body=BodyBone(scene,setup);var joints=new HashSet<int>();
        var clearances=pins.Select(p=>p.Clearance).OfType<ContactClearance>().GroupBy(p=>(p.Normal,p.Height)).Select(g=>new ContactClearance(g.Key.Normal,g.Key.Height,g.SelectMany(p=>p.Points).Distinct().ToArray()){SupportPoints=g.SelectMany(p=>p.SupportPoints).Distinct().ToArray()}).ToArray();
        if(clearances.Length>0)
        {
            // A long target forearm/leg can hit the floor although it was clear on the source.
            // Allow reviewed existing anatomical joints to avoid these collisions during baking.
            var mapped=setup.Chains.SelectMany(c=>new[]{c.Root,c.Middle,c.Effector}).Concat(setup.BoneRoles.Where(r=>r.SemanticRole is "Hips" or "Spine" or "Chest" or "Neck" or "Head"||r.SemanticRole.StartsWith("Clavicle.",StringComparison.Ordinal)).Select(r=>r.Bone)).Append(body).Where(i=>i>=0).ToHashSet();
            if(broadJoints)joints.UnionWith(mapped);
            else foreach(var floor in clearances)foreach(var point in floor.Points.Where(p=>Vector3.Dot(ContactGeometry.Point(p,pose),floor.Normal)<floor.Height-tolerance))for(int j=0;j<point.InfluenceCount;j++)if(point.WeightAt(j)>0)
            {
                for(int bone=(int)point.JointAt(j);bone>=0&&bone!=body;bone=scene.Bones[bone].Parent)if(mapped.Contains(bone))joints.Add(bone);
            }
            joints.ExceptWith(setup.PreservedHelperBones);
        }
        foreach(var pin in pins)
        {
            var chain=setup.Chains.FirstOrDefault(c=>c.Effector==pin.Bone);
            if(chain is not null){joints.Add(chain.Root);joints.Add(chain.Middle);if(pin.HoldRotation||pin.LocalPoint!=Vector3.Zero||pin.SurfacePoints.Length>0)joints.Add(pin.Bone);}
            else for(int i=pin.Bone;i>=0&&i!=body;i=scene.Bones[i].Parent)joints.Add(i);
        }
        if(adjustBody&&body>=0)joints.Add(body);
        joints.ExceptWith(edits.Keys);
        var variables=joints.Order().SelectMany(b=>Enumerable.Range(0,3).Select(axis=>(Bone:b,Axis:axis,Translation:false))).ToList();
        if(adjustBody&&body>=0&&!edits.ContainsKey(body))variables.AddRange(Enumerable.Range(0,3).Select(axis=>(Bone:body,Axis:axis,Translation:true)));
        var seedLocal=local.ToDictionary();var q=new double[variables.Count];
        EvaluatedPose Evaluate(double[] values)
        {
            foreach(var b in joints)local[b]=seedLocal[b];
            if(adjustBody&&body>=0&&!edits.ContainsKey(body))local[body]=seedLocal[body];
            for(int i=0;i<variables.Count;i++){var v=variables[i];var t=local[v.Bone];if(v.Translation){var p=t.Translation;p[v.Axis]+=(float)values[i]*unit;local[v.Bone]=t with{Translation=p};}else{var r=t.Rotation;r[v.Axis]+=(float)values[i]*180/MathF.PI;local[v.Bone]=t with{Rotation=r};}}
            return EvaluateLocal(local);
        }
        var bodyBones=PoseGeometry.BodyAdjustmentBones(setup);bool bodyFitting=clearances.Any(c=>c.SupportPoints.Length>0);
        var referenceRotations=referencePose is null?null:Enumerable.Range(0,scene.Bones.Length).Select(b=>PoseGeometry.FittingRotation(scene,referencePose,b,bodyBones,bodyFitting)).ToArray();
        double[] Residual(EvaluatedPose p)
        {
            var result=new List<double>();foreach(var pin in pins)
            {
                var delta=ContactGeometry.Residual(pin,p)/unit;
                if(adaptAutomatic&&pin.SoftSupport&&Vector3.Dot(delta,pin.PlaneNormal??Vector3.UnitY)>0)delta=Vector3.Zero;
                result.AddRange(new double[]{delta.X,delta.Y,delta.Z});
                if(pin.AllowedSurface is not null&&ContactGeometry.Normal(pin,p) is null){var bound=ContactGeometry.AllowedSurfaceResidual(pin,p)/unit;result.AddRange(new double[]{bound.X,bound.Y,bound.Z});}
                if(pin.HoldRotation){var a=PoseGeometry.Rotation(p.World[pin.Bone]);var d=Quaternion.Normalize(Quaternion.Concatenate(Quaternion.Inverse(ContactGeometry.TargetRotation(pin,p)),a));if(d.W<0)d=new(-d.X,-d.Y,-d.Z,-d.W);var v=new Vector3(d.X,d.Y,d.Z)*2;result.AddRange(new double[]{v.X,v.Y,v.Z});}
            }
            foreach(var floor in clearances)foreach(var point in floor.Points)result.Add(Math.Min(0,Vector3.Dot(ContactGeometry.Point(point,p),floor.Normal)-floor.Height)/unit);
            if(adaptable)foreach(var floor in clearances)result.Add(ContactGeometry.SupportHeight(floor,p)/unit);
            return result.ToArray();
        }
        double[] PoseResidual(EvaluatedPose p)
        {
            var result=new List<double>();if(referencePose is not null)foreach(int bone in joints)
            {
                // Ordinary fitting preserves world articulation. Supported body
                // fitting lets limbs follow a deliberate torso/pelvis adjustment.
                // Native authored winding remains in the resulting Euler keys.
                var d=Quaternion.Normalize(Quaternion.Concatenate(Quaternion.Inverse(referenceRotations![bone]),PoseGeometry.FittingRotation(scene,p,bone,bodyBones,bodyFitting)));
                if(d.W<0)d=new(-d.X,-d.Y,-d.Z,-d.W);
                double weight=bodyFitting&&bodyBones.Contains(bone)?.35:1;
                result.AddRange(new double[]{weight*2*d.X,weight*2*d.Y,weight*2*d.Z});
            }
            // Nearby inferred patches should keep their correspondence when it
            // costs little articulation. A bounded redescending preference must
            // not fold a body to recover a distant inferred hip/head observation.
            // It is secondary only; authored/required pins stay in the task.
            if(referencePose is not null&&adaptAutomatic)foreach(var pin in pins.Where(p=>p.Automatic&&p.SoftSupport&&!p.ClearanceOnly&&!p.RequiredSupport))
            {
                var d=ContactGeometry.Residual(pin,p)/unit;double distance=Math.Max(0,Vector3.Dot(d,pin.PlaneNormal??Vector3.UnitY));
                bool limb=pin.Clearance?.SupportPoints.Length>0&&(pin.Role.StartsWith("Hand.",StringComparison.Ordinal)||pin.Role.StartsWith("Foot.",StringComparison.Ordinal));
                result.Add(limb?20*distance:Math.Tanh(distance/.02));
            }
            return result.ToArray();
        }
        static double Cost(double[] e)=>e.Sum(v=>v*v);
        (float Position,float Rotation) Errors(EvaluatedPose p)
        {
            float position=pins.Max(pin=>Math.Max(adaptAutomatic&&pin.SoftSupport?0:Math.Max(ContactGeometry.Residual(pin,p).Length(),ContactGeometry.AllowedSurfaceResidual(pin,p).Length()),ContactGeometry.Penetration(pin,p)));
            if(adaptable)position=Math.Max(position,PositionError(pins,p));
            float rotation=pins.Where(pin=>pin.HoldRotation).Select(pin=>2*MathF.Acos(Math.Clamp(Math.Abs(Quaternion.Dot(PoseGeometry.Rotation(p.World[pin.Bone]),ContactGeometry.TargetRotation(pin,p))),0,1))).DefaultIfEmpty().Max();
            return(position,rotation);
        }
        float positionError=0,rotationError=0;int step=0;
        double[]? feasible=null;double bestPoseCost=double.PositiveInfinity;int firstFeasible=-1;
        for(;step<=iterations;step++)
        {
            pose=Evaluate(q);(positionError,rotationError)=Errors(pose);var poseError=PoseResidual(pose);double poseCost=Cost(poseError);
            bool satisfied=positionError<=tolerance&&rotationError<=.001f;
            if(satisfied)
            {
                if(poseCost<bestPoseCost){feasible=(double[])q.Clone();bestPoseCost=poseCost;}
                if(firstFeasible<0)firstFeasible=step;
                // Small, already feasible corrections need no expensive
                // secondary solve. This is a work bound, not contact acceptance.
                double articulation=0;if(referencePose is not null)for(int j=0;j<joints.Count*3;j+=3)articulation=Math.Max(articulation,Math.Sqrt(poseError[j]*poseError[j]+poseError[j+1]*poseError[j+1]+poseError[j+2]*poseError[j+2]));
                bool limbGap=pins.Any(p=>p.Automatic&&p.SoftSupport&&p.Clearance?.SupportPoints.Length>0&&(p.Role.StartsWith("Hand.",StringComparison.Ordinal)||p.Role.StartsWith("Foot.",StringComparison.Ordinal))&&ContactGeometry.Residual(p,pose).Length()>tolerance);
                if(referencePose is null||!limbGap&&articulation<=2*Math.Sin(5*Math.PI/360)||poseCost<1e-8||step-firstFeasible>=8)break;
            }
            if(step==iterations||variables.Count==0)break;
            var error=Residual(pose);int rows=error.Length,n=variables.Count;var jac=new double[rows,n];const double epsilon=.001;
            var poseJac=new double[poseError.Length,n];
            for(int j=0;j<n;j++)
            {
                q[j]+=epsilon;var plusPose=Evaluate(q);var plus=Residual(plusPose);var plusArticulation=PoseResidual(plusPose);
                q[j]-=2*epsilon;var minusPose=Evaluate(q);var minus=Residual(minusPose);var minusArticulation=PoseResidual(minusPose);q[j]+=epsilon;
                for(int r=0;r<rows;r++)jac[r,j]=(plus[r]-minus[r])/(2*epsilon);
                for(int r=0;r<poseError.Length;r++)poseJac[r,j]=(plusArticulation[r]-minusArticulation[r])/(2*epsilon);
            }
            var (poseNormal,poseRight)=NormalEquations(poseJac,poseError,.03);var poseDelta=referencePose is null?new double[n]:LinearSolve(poseNormal,poseRight)??new double[n];
            bool improved=false;
            foreach(double damping in new[]{.003,.02,.1})
            {
                var (normal,rhs)=NormalEquations(jac,error,damping);
                var delta=LinearSolve((double[,])normal.Clone(),rhs);if(delta is null)continue;
                // Project the pose step into the task Jacobian's damped null
                // space. Contact accuracy has priority; pose fidelity never
                // fades away or replaces a required constraint with a weight.
                var leakage=new double[n];for(int j=0;j<n;j++)for(int k=0;k<n;k++)leakage[j]+=(normal[j,k]-(j==k?damping*damping:0))*poseDelta[k];
                var taskPart=LinearSolve((double[,])normal.Clone(),leakage)??new double[n];
                if(satisfied)for(int j=0;j<n;j++)delta[j]+=.5*(poseDelta[j]-taskPart[j]);
                double max=delta.Select(Math.Abs).DefaultIfEmpty().Max();if(max>.25)for(int j=0;j<n;j++)delta[j]*=.25/max;
                foreach(double fraction in new[]{1.0,.5,.25,.125})
                {
                    var candidate=q.Select((v,i)=>v+delta[i]*fraction).ToArray();var candidatePose=Evaluate(candidate);var candidateError=Residual(candidatePose);
                    if(satisfied&&referencePose is not null)
                    {
                        // One frozen-Jacobian projection removes nonlinear task
                        // leakage before judging a secondary articulation step.
                        var correction=new double[n];for(int j=0;j<n;j++)for(int r=0;r<rows;r++)correction[j]-=jac[r,j]*candidateError[r];
                        var repair=LinearSolve((double[,])normal.Clone(),correction);if(repair is not null){for(int j=0;j<n;j++)candidate[j]+=repair[j];candidatePose=Evaluate(candidate);candidateError=Residual(candidatePose);}
                    }
                    var candidateBounds=Errors(candidatePose);
                    bool articulationAllowed=true;if(satisfied&&referencePose is not null)foreach(int bone in joints)
                    {
                        if(bodyFitting&&bodyBones.Contains(bone))continue;
                        float Angle(EvaluatedPose value)=>PoseGeometry.RotationDifference(referenceRotations![bone],PoseGeometry.FittingRotation(scene,value,bone,bodyBones,bodyFitting));
                        float angle=Angle(candidatePose);if(!float.IsFinite(angle)||angle>Math.Max(RetargetAcceptance.MaximumAutomaticCorrectionDegrees,Angle(pose))+.0001f){articulationAllowed=false;break;}
                    }
                    bool better=satisfied?articulationAllowed&&candidateBounds.Position<=tolerance&&candidateBounds.Rotation<=.001f&&Cost(PoseResidual(candidatePose))<poseCost-1e-12:Cost(candidateError)<Cost(error)-1e-14;
                    if(better){q=candidate;improved=true;break;}
                }
                if(improved)break;
            }
            if(!improved)break;
        }
        pose=Evaluate(feasible??q);(positionError,rotationError)=Errors(pose);
        if(positionError>tolerance||rotationError>.001f)
        {
            if(clearances.Length>0&&!broadJoints)return Solve(scene,input,setup,edits,pins,adjustBody,iterations,adaptAutomatic,true,referencePose);
            var conflicts=pins.Select(p=>$"{p.Role} on '{p.Surface}' [{p.Id}]: position {ContactGeometry.Residual(p,pose).Length():G6}, penetration {ContactGeometry.Penetration(p,pose):G6}");
            return Fail("CONTACT_UNRESOLVED",$"Contact solver did not converge in {step} iterations ({string.Join("; ",conflicts)}); max rotation error {rotationError*180/MathF.PI:G5} degrees. This is not proof of incompatible anatomy. No stretching, automatic release or source edit was applied; review the seed, constraints and permitted body adjustment.");
        }
        var changes=local.Where(p=>p.Value!=input.Local[p.Key]).ToDictionary();return new(new(changes,positionError,rotationError,step),[],[]);
    }
    internal static double[]? LinearSolve(double[,] a,double[] b)
    {
        int n=b.Length;for(int c=0;c<n;c++){int pivot=c;for(int r=c+1;r<n;r++)if(Math.Abs(a[r,c])>Math.Abs(a[pivot,c]))pivot=r;if(Math.Abs(a[pivot,c])<1e-15)return null;
            if(pivot!=c){for(int j=c;j<n;j++)(a[c,j],a[pivot,j])=(a[pivot,j],a[c,j]);(b[c],b[pivot])=(b[pivot],b[c]);}
            double divisor=a[c,c];for(int j=c;j<n;j++)a[c,j]/=divisor;b[c]/=divisor;
            for(int r=0;r<n;r++)if(r!=c){double f=a[r,c];for(int j=c;j<n;j++)a[r,j]-=f*a[c,j];b[r]-=f*b[c];}}
        return b;
    }
    internal static (double[,] Matrix,double[] Right) NormalEquations(double[,] jac,double[] error,double damping)
    {
        int n=jac.GetLength(1);var matrix=new double[n,n];var right=new double[n];var active=new int[n];
        // Surface rows touch few anatomical variables. Skip exact zeros without
        // changing each sum's sample order or pruning small valid derivatives.
        for(int row=0;row<error.Length;row++)
        {
            int count=0;for(int j=0;j<n;j++)if(jac[row,j]!=0)active[count++]=j;
            for(int a=0;a<count;a++){int j=active[a];right[j]-=jac[row,j]*error[row];for(int b=0;b<count;b++){int k=active[b];matrix[j,k]+=jac[row,j]*jac[row,k];}}
        }
        for(int j=0;j<n;j++)matrix[j,j]+=damping*damping;return(matrix,right);
    }
}
