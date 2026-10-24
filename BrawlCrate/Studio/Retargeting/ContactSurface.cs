using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace BrawlCrate.Studio.Retargeting
{
    public sealed class SkinWeight
    {
        public int Joint { get; set; }
        public Vector3 Local { get; set; }
        public float Weight { get; set; }
    }
    public sealed class SkinPoint
    {
        public SkinWeight[] Weights { get; set; }
        public Vector3 Evaluate(RigPose pose)
        {
            Vector3 result = Vector3.Zero;
            foreach (SkinWeight w in Weights) result += Vector3.Transform(w.Local, pose.World[w.Joint]) * w.Weight;
            return result;
        }
    }
    public sealed class SurfaceSample
    {
        public Vector3 Point { get; internal set; }
        public float Clearance { get; internal set; }
        public bool Flat { get; internal set; }
        public float Flatness { get; internal set; }
    }
    public sealed class ContactSurface
    {
        public string Role { get; set; }
        private SkinPoint[] points;
        private SkinSupportTree support;
        public SkinPoint[] Points {get=>points;set{points=value;support=null;}}
        public Vector3 NormalLocal { get; set; }
        public Vector3 ForwardLocal { get; set; }
        public float Clearance(RigPose pose)
        {
            LowestPoint(pose,out float minimum);
            return minimum-pose.Rig.GroundHeight;
        }
        internal SkinPoint LowestPoint(RigPose pose,out float minimum)
        {
            if(points.Length>32)return (support??(support=new SkinSupportTree(points,pose.Rig))).Lowest(pose,out minimum);
            minimum=float.MaxValue;SkinPoint result=null;
            foreach(var point in points){float height=Vector3.Dot(point.Evaluate(pose),pose.Rig.GroundNormal);if(height<minimum){minimum=height;result=point;}}
            return result;
        }
        public SkinPoint Marker(RigPose pose)
        {
            float minimum=Points.Min(p=>Vector3.Dot(p.Evaluate(pose),pose.Rig.GroundNormal));
            float band=Math.Max(0.01f,Vector3.Distance(pose.Rig.Rest.Point("left.upperLeg"),pose.Rig.Rest.Point("left.foot"))*0.008f);
            SkinPoint[] patch=Points.Where(p=>Vector3.Dot(p.Evaluate(pose),pose.Rig.GroundNormal)<=minimum+band).ToArray();
            var weights=patch.SelectMany(p=>p.Weights).GroupBy(w=>w.Joint).Select(g=> {
                float total=g.Sum(w=>w.Weight);
                return new SkinWeight{Joint=g.Key,Weight=total/patch.Length,Local=g.Aggregate(Vector3.Zero,(v,w)=>v+w.Local*w.Weight)/total};
            }).ToArray();
            return new SkinPoint{Weights=weights};
        }
        public SurfaceSample Sample(RigPose pose)
        {
            Rig rig = pose.Rig; Vector3 up = rig.GroundNormal;
            Vector3[] points = Points.Select(p => p.Evaluate(pose)).ToArray();
            float minimum = points.Min(p => Vector3.Dot(p, up));
            float band = Math.Max(0.01f, Vector3.Distance(rig.Rest.Point("left.upperLeg"), rig.Rest.Point("left.foot")) * 0.008f);
            Vector3[] patch = points.Where(p => Vector3.Dot(p, up) <= minimum + band).ToArray();
            Vector3 center = patch.Aggregate(Vector3.Zero, (a,b) => a+b) / patch.Length;
            float flatness;
            if (Role.EndsWith(".foot", StringComparison.Ordinal))
            {
                Vector3 forward = Vector3.Transform(ForwardLocal, pose.Orientation[rig.Roles[Role]]);
                float middle = points.Average(p => Vector3.Dot(p, forward));
                var heel = points.Where(p => Vector3.Dot(p, forward) <= middle).ToArray();
                var toe = points.Where(p => Vector3.Dot(p, forward) >= middle).ToArray();
                float a = heel.Min(p => Vector3.Dot(p,up)), b = toe.Min(p => Vector3.Dot(p,up));
                float tilt=RigMath.Clamp((float)(Math.Abs(a-b)/(band*6)),0,1);
                flatness=1-tilt*tilt*(3-2*tilt);
                float normal=Vector3.Dot(Vector3.Transform(NormalLocal,pose.Orientation[rig.Roles[Role]]),up);
                flatness*=RigMath.Clamp((normal-0.92f)/0.07f,0,1);
            }
            else if(Role.EndsWith(".hand",StringComparison.Ordinal))flatness=RigMath.Clamp((Math.Abs(Vector3.Dot(Vector3.Transform(NormalLocal,pose.Orientation[rig.Roles[Role]]),up))-0.85f)/0.13f,0,1);
            else flatness=0;
            return new SurfaceSample {Point=center, Clearance=minimum-rig.GroundHeight, Flat=flatness>0.95f,Flatness=flatness};
        }
    }
}
