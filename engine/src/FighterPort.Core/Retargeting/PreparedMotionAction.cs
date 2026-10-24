using System.Numerics;
using System.Runtime.CompilerServices;

namespace FighterPort.Core;

// Native-bone witnesses remain independent of physical optimization weights.
// An unweighted helper is still measured and retained in native channel data.
public record MotionActionMeasurement(int Landmarks,double MeanPositionScaleSquared,double MeanOrientationRadiansSquared,
    float MaximumPosition,float MaximumPositionScaleFraction,float MaximumOrientationRadians,int PositionBone,int OrientationBone);

// Offline action preference. Rest-surface area and skin influences define
// physical weight, rather than the number of names/joints in a native graph.
// Model axes remain fixed: free body translation cannot erase facing error.
public sealed class PreparedMotionAction
{
    public const string Version="area-weighted-skin-moment-world-orientation-2";
    public const double PositionWeight=1,OrientationWeight=1;
    readonly record struct Landmark(int Bone,int Origin,Vector3 Position,Quaternion Orientation);
    readonly record struct FactorTerm(int Row,double Weight);
    readonly record struct Edge(int Parent,int Child,double Length);
    sealed record Geometry(int[] Bones,int[] Origins,FactorTerm[][] Factors,Edge[] Edges,
        double[] BoneMeasure,Vector3 Center,float Scale,double SurfaceArea,double DiscardedTraceFraction);
    static readonly ConditionalWeakTable<RigDescriptor,Geometry> PreparedGeometry=new();
    readonly Landmark[] landmarks;
    readonly (int Landmark,double Weight)[] orientationTasks;
    readonly Geometry geometry;
    readonly EvaluatedPose reference;
    readonly Scene scene;
    readonly int boneCount;
    public IReadOnlyList<int> TargetBones {get;}
    public int LandmarkCount=>landmarks.Length;
    public float CharacteristicLength=>geometry.Scale;
    public int PositionResidualCount=>geometry.SurfaceArea>0?geometry.Factors.Length*3:geometry.Edges.Length*6;
    public int ResidualCount=>PositionResidualCount+orientationTasks.Length*3;
    public string PositionMeasure=>geometry.SurfaceArea>0?"Rest triangle area-lumped linear-blend skin":"Rest-edge two-point quadrature";
    // EvaluatedPose currently carries geometry layers, not the viewer's VIS0/
    // object-toggle state. Match the all-retained-skin clearance contract and
    // report this limitation instead of claiming an exact displayed-body cost.
    public string VisibilityPolicy=>"All retained native meshes; animated visibility and viewer object toggles are not represented";
    public int DefaultHiddenMeshCount=>scene.Meshes.Count(m=>m.DrawCalls.Length>0&&m.DrawCalls.All(c=>!c.DefaultVisible));
    public string GeometryContext=>reference.GeometryPositions is null?"Native rest vertex data":"Prepared geometry-layer vertex data";
    public double SurfaceArea=>geometry.SurfaceArea;
    public double MomentDiscardedTraceFraction=>geometry.DiscardedTraceFraction;

    PreparedMotionAction(Scene scene,Landmark[] landmarks,Geometry geometry,EvaluatedPose reference)
    {
        this.scene=scene;this.landmarks=landmarks;this.geometry=geometry;
        this.reference=reference with{World=reference.World.ToArray(),Skin=reference.Skin.ToArray(),GeometryPositions=reference.GeometryPositions?.Select(p=>p.ToArray()).ToArray()};
        boneCount=scene.Bones.Length;
        TargetBones=Array.AsReadOnly(landmarks.Select(p=>p.Bone).ToArray());
        double total=landmarks.Sum(p=>geometry.BoneMeasure[p.Bone]);
        orientationTasks=total<=0?[]:landmarks.Select((p,i)=>(Landmark:i,Weight:geometry.BoneMeasure[p.Bone]/total))
            .Where(p=>p.Weight>0).Select(p=>(p.Landmark,Math.Sqrt(OrientationWeight*p.Weight))).ToArray();
    }

    public static PreparedMotionAction Create(RigDescriptor rig,RetargetMappingReport mapping,EvaluatedPose reference)
    {
        int count=rig.Scene.Bones.Length;
        if(reference.World.Length!=count||reference.Skin.Length!=count)throw new ArgumentException("Action reference belongs to another native bone graph.",nameof(reference));
        var bones=mapping.Bones.Select(p=>p.Target).ToArray();
        if(bones.Length==0||bones.Distinct().Count()!=bones.Length||bones.Any(b=>b<0||b>=count))
            throw new ArgumentException("Action preparation needs unique valid mapped target bones.",nameof(mapping));
        int root=rig.Binding.Roles["RootMotion"],body=rig.Binding.Roles.GetValueOrDefault("Hips",root);
        if(root<0||root>=count||body<0||body>=count)throw new InvalidDataException("Action origin binding is outside the native graph.");
        var landmarks=bones.OrderBy(b=>rig.CanonicalBoneRanks[b]).Select(b=>
        {
            int origin=PoseGeometry.Ancestor(rig.Scene,body,b)?body:root;
            var position=reference.World[b].Translation-reference.World[origin].Translation;
            var orientation=PoseGeometry.Rotation(reference.World[b]);
            if(!PoseGeometry.Finite(position)||!Finite(orientation))throw new InvalidDataException("Action reference has a nonfinite position/orientation.");
            return new Landmark(b,origin,position,orientation);
        }).ToArray();
        // Rest measure is shared across clips. A prepared morph frame changes
        // point moments while its rest-area/skin ownership remains unchanged.
        var geometry=reference.GeometryPositions is null
            ?PreparedGeometry.GetValue(rig,r=>BuildGeometry(r,null))
            :BuildGeometry(rig,reference.GeometryPositions);
        return new(rig.Scene,landmarks,geometry,reference);
    }

    // Fixed dimensionality/order for one prepared request. A coefficient
    // solver differentiates this vector once per native channel, then applies
    // destination interpolation coefficients. Cost is its exact squared norm.
    public double[] Residuals(EvaluatedPose pose)
    {
        Validate(pose);
        var result=new double[ResidualCount];int output=0;
        if(geometry.SurfaceArea>0)
        {
            var differences=new double[geometry.Bones.Length*4,3];var center=geometry.Center;double scale=geometry.Scale;
            for(int i=0;i<geometry.Bones.Length;i++)
            {
                int bone=geometry.Bones[i],origin=geometry.Origins[bone];var a=pose.Skin[bone];var b=reference.Skin[bone];
                var movement=pose.World[origin].Translation-reference.World[origin].Translation;
                int row=i*4;
                differences[row,0]=(double)a.M11-b.M11;differences[row,1]=(double)a.M12-b.M12;differences[row,2]=(double)a.M13-b.M13;
                differences[row+1,0]=(double)a.M21-b.M21;differences[row+1,1]=(double)a.M22-b.M22;differences[row+1,2]=(double)a.M23-b.M23;
                differences[row+2,0]=(double)a.M31-b.M31;differences[row+2,1]=(double)a.M32-b.M32;differences[row+2,2]=(double)a.M33-b.M33;
                differences[row+3,0]=(center.X*differences[row,0]+center.Y*differences[row+1,0]+center.Z*differences[row+2,0]+(double)a.M41-b.M41-movement.X)/scale;
                differences[row+3,1]=(center.X*differences[row,1]+center.Y*differences[row+1,1]+center.Z*differences[row+2,1]+(double)a.M42-b.M42-movement.Y)/scale;
                differences[row+3,2]=(center.X*differences[row,2]+center.Y*differences[row+1,2]+center.Z*differences[row+2,2]+(double)a.M43-b.M43-movement.Z)/scale;
            }
            double weight=Math.Sqrt(PositionWeight);
            foreach(var factor in geometry.Factors)for(int axis=0;axis<3;axis++)
            {
                double value=0;foreach(var term in factor)value+=term.Weight*differences[term.Row,axis];
                result[output++]=weight*value;
            }
        }
        else
        {
            // Explicit geometry-free fallback integrates articulated edges,
            // never equal masses for extra coincident helper names.
            double length=geometry.Edges.Sum(e=>e.Length);
            foreach(var edge in geometry.Edges)
            {
                var a=RelativeDifference(edge.Parent,pose);var b=RelativeDifference(edge.Child,pose);
                double weight=Math.Sqrt(PositionWeight*edge.Length/(2*length))/geometry.Scale;
                foreach(double t in new[]{.5-1/(2*Math.Sqrt(3)),.5+1/(2*Math.Sqrt(3))})
                    for(int axis=0;axis<3;axis++)result[output++]=weight*((1-t)*a[axis]+t*b[axis]);
            }
        }
        foreach(var task in orientationTasks)
        {
            var landmark=landmarks[task.Landmark];var value=RotationVector(PoseGeometry.Rotation(pose.World[landmark.Bone]),landmark.Orientation);
            result[output++]=value.X*task.Weight;result[output++]=value.Y*task.Weight;result[output++]=value.Z*task.Weight;
        }
        if(result.Any(x=>!double.IsFinite(x)))throw new InvalidDataException("Action candidate produced a nonfinite geometric residual.");
        return result;
    }
    Vector3 RelativeDifference(int bone,EvaluatedPose pose)
    {
        int origin=geometry.Origins[bone];
        return pose.World[bone].Translation-pose.World[origin].Translation
            -(reference.World[bone].Translation-reference.World[origin].Translation);
    }
    public MotionActionMeasurement Measure(EvaluatedPose pose)
    {
        var residual=Residuals(pose);
        float maximumPosition=0,maximumAngle=0;int positionBone=-1,orientationBone=-1;
        foreach(var landmark in landmarks)
        {
            var relative=pose.World[landmark.Bone].Translation-pose.World[landmark.Origin].Translation;
            var orientation=PoseGeometry.Rotation(pose.World[landmark.Bone]);
            if(!PoseGeometry.Finite(relative)||!Finite(orientation))throw new InvalidDataException("Action candidate has a nonfinite position/orientation.");
            float distance=Vector3.Distance(relative,landmark.Position);
            float angle=RotationVector(orientation,landmark.Orientation).Length();
            if(distance>maximumPosition){maximumPosition=distance;positionBone=landmark.Bone;}
            if(angle>maximumAngle){maximumAngle=angle;orientationBone=landmark.Bone;}
        }
        return new(landmarks.Length,residual.Take(PositionResidualCount).Sum(x=>x*x)/PositionWeight,
            residual.Skip(PositionResidualCount).Sum(x=>x*x)/OrientationWeight,
            maximumPosition,maximumPosition/geometry.Scale,maximumAngle,positionBone,orientationBone);
    }
    public double Cost(EvaluatedPose pose)=>Residuals(pose).Sum(x=>x*x);
    void Validate(EvaluatedPose pose)
    {
        if(pose.World.Length!=boneCount||pose.Skin.Length!=boneCount)throw new ArgumentException("Action candidate belongs to another native bone graph.",nameof(pose));
        // Candidate bones may change; silently reusing a moment factor for a
        // different morph frame would measure the wrong physical geometry.
        if(!ReferenceEquals(pose.GeometryPositions,reference.GeometryPositions))
            for(int m=0;m<scene.Meshes.Length;m++)for(int v=0;v<scene.Meshes[m].Vertices.Length;v++)
            {
                var a=pose.GeometryPositions is {} ga?ga[m][v]:scene.Meshes[m].Vertices[v].Position;
                var b=reference.GeometryPositions is {} gb?gb[m][v]:scene.Meshes[m].Vertices[v].Position;
                if(a!=b)throw new NotSupportedException("Action skin moments require the same prepared geometry-layer frame; changed morph geometry needs a new action reference.");
            }
    }
    static Geometry BuildGeometry(RigDescriptor rig,Vector3[][]? positions)
    {
        var scene=rig.Scene;int count=scene.Bones.Length;float scale=rig.CharacteristicLength;
        if(!float.IsFinite(scale)||scale<=0)throw new InvalidDataException("Action preparation needs a finite positive geometric scale.");
        int root=rig.Binding.Roles["RootMotion"],body=rig.Binding.Roles.GetValueOrDefault("Hips",root);
        var origins=Enumerable.Range(0,count).Select(b=>PoseGeometry.Ancestor(scene,body,b)?body:root).ToArray();
        var center=rig.Rest.World[body].Translation;var mass=new double[count];double area=0;
        var measures=scene.Meshes.Select(m=>new double[m.Vertices.Length]).ToArray();
        for(int m=0;m<scene.Meshes.Length;m++)
        {
            var mesh=scene.Meshes[m];
            for(int t=0;t<mesh.Indices.Length;t+=3)
            {
                int a=(int)mesh.Indices[t],b=(int)mesh.Indices[t+1],c=(int)mesh.Indices[t+2];
                double value=Vector3.Cross(mesh.Vertices[b].Position-mesh.Vertices[a].Position,mesh.Vertices[c].Position-mesh.Vertices[a].Position).Length()/2d;
                if(!double.IsFinite(value))throw new InvalidDataException("Action rest geometry has nonfinite triangle area.");
                area+=value;measures[m][a]+=value/3;measures[m][b]+=value/3;measures[m][c]+=value/3;
            }
            for(int v=0;v<mesh.Vertices.Length;v++)for(int j=0;j<4;j++)
                if(mesh.Vertices[v].Weights[j]>0)mass[(int)mesh.Vertices[v].Joints[j]]+=measures[m][v]*mesh.Vertices[v].Weights[j];
        }
        if(area<=0)
        {
            var edges=Enumerable.Range(0,count).Where(b=>scene.Bones[b].Parent>=0).Select(b=>new Edge(scene.Bones[b].Parent,b,
                Vector3.Distance(rig.Rest.World[b].Translation,rig.Rest.World[scene.Bones[b].Parent].Translation))).Where(e=>e.Length>0)
                .OrderBy(e=>rig.CanonicalBoneRanks[e.Child]).ToArray();
            foreach(var edge in edges){mass[edge.Parent]+=edge.Length/2;mass[edge.Child]+=edge.Length/2;}
            if(edges.Length==0)throw new NotSupportedException("Action geometry needs positive-area skin or a nonzero articulated edge; no equal-name-count weights were invented.");
            return new([],origins,[],edges,mass,center,scale,0,0);
        }
        var bones=Enumerable.Range(0,count).Where(b=>mass[b]>0).OrderBy(b=>rig.CanonicalBoneRanks[b]).ToArray();
        var index=bones.Select((b,i)=>(b,i)).ToDictionary(p=>p.b,p=>p.i);int dimension=bones.Length*4;
        var moment=new double[dimension,dimension];
        for(int m=0;m<scene.Meshes.Length;m++)for(int v=0;v<scene.Meshes[m].Vertices.Length;v++)
        {
            double weight=measures[m][v]/area;if(weight==0)continue;var vertex=scene.Meshes[m].Vertices[v];
            var point=((positions is null?vertex.Position:positions[m][v])-center)/scale;
            if(!PoseGeometry.Finite(point))throw new InvalidDataException("Action geometry layer has a nonfinite position.");
            double[] coordinates=[point.X,point.Y,point.Z,1];
            var influences=new Dictionary<int,double>();
            for(int j=0;j<4;j++)if(vertex.Weights[j]>0){int bone=(int)vertex.Joints[j];influences[bone]=influences.GetValueOrDefault(bone)+vertex.Weights[j];}
            var ordered=influences.OrderBy(p=>index[p.Key]).ToArray();
            foreach(var a in ordered)foreach(var b in ordered)
            {
                double jointWeight=weight*a.Value*b.Value;int row=index[a.Key]*4,column=index[b.Key]*4;
                for(int i=0;i<4;i++)for(int j=0;j<4;j++)moment[row+i,column+j]+=jointWeight*coordinates[i]*coordinates[j];
            }
        }
        // Factor the complete area-lumped Gram moment, including blended-skin
        // cross terms. Expose roundoff-scale discarded trace; sparse action
        // measurements are not continuous collision/native acceptance.
        var factor=new double[dimension,dimension];var rows=new List<FactorTerm[]>();
        double trace=0;for(int i=0;i<dimension;i++)trace+=moment[i,i];double discarded=0;
        for(int column=0;column<dimension;column++)
        {
            double pivot=moment[column,column];for(int k=0;k<column;k++)pivot-=factor[column,k]*factor[column,k];
            if(pivot< -trace*1e-10)throw new InvalidDataException("Action skin moment factor is numerically indefinite.");
            if(pivot<=trace*1e-13){discarded+=Math.Max(0,pivot);continue;}
            factor[column,column]=Math.Sqrt(pivot);
            for(int row=column+1;row<dimension;row++)
            {
                double value=moment[row,column];for(int k=0;k<column;k++)value-=factor[row,k]*factor[column,k];
                factor[row,column]=value/factor[column,column];
            }
            rows.Add(Enumerable.Range(column,dimension-column).Where(r=>factor[r,column]!=0)
                .Select(r=>new FactorTerm(r,factor[r,column])).ToArray());
        }
        return new(bones,origins,rows.ToArray(),[],mass,center,scale,area,trace>0?discarded/trace:0);
    }
    internal static Vector3 RotationVector(Quaternion value,Quaternion reference)
    {
        if(value==reference||value==-reference)return Vector3.Zero;
        double x=-(double)value.W*reference.X+(double)value.X*reference.W-(double)value.Y*reference.Z+(double)value.Z*reference.Y;
        double y=-(double)value.W*reference.Y+(double)value.X*reference.Z+(double)value.Y*reference.W-(double)value.Z*reference.X;
        double z=-(double)value.W*reference.Z-(double)value.X*reference.Y+(double)value.Y*reference.X+(double)value.Z*reference.W;
        double w=(double)value.W*reference.W+(double)value.X*reference.X+(double)value.Y*reference.Y+(double)value.Z*reference.Z;
        if(w<0){x=-x;y=-y;z=-z;w=-w;}
        double length=Math.Sqrt(x*x+y*y+z*z),factor=length>1e-30?2*Math.Atan2(length,w)/length:0;
        return new((float)(x*factor),(float)(y*factor),(float)(z*factor));
    }
    static bool Finite(Quaternion value)=>float.IsFinite(value.X)&&float.IsFinite(value.Y)&&float.IsFinite(value.Z)&&float.IsFinite(value.W);
}
