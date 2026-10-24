using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace BrawlCrate.Studio.Retargeting
{
    // Conservative bounds on the linear weighted-skin coefficients. Leaves use
    // the original float evaluator; no contact geometry is removed or sampled.
    internal sealed class SkinSupportTree
    {
        private struct Coefficient
        {
            internal int Joint;
            internal double X,Y,Z,W;
            internal double At(int axis)=>axis==0?X:axis==1?Y:axis==2?Z:W;
        }
        private sealed class Point
        {
            internal SkinPoint Skin;
            internal int Index;
            internal Vector3 Rest;
            internal Coefficient[] Coefficients;
        }
        private sealed class Bound
        {
            internal int Joint,Seen;
            internal readonly double[] Minimum={double.PositiveInfinity,double.PositiveInfinity,double.PositiveInfinity,double.PositiveInfinity};
            internal readonly double[] Maximum={double.NegativeInfinity,double.NegativeInfinity,double.NegativeInfinity,double.NegativeInfinity};
        }
        private sealed class Node
        {
            internal int Start,Count;
            internal Bound[] Bounds;
            internal Node Left,Right;
        }
        private readonly Point[] points;
        private readonly Node root;
        internal SkinSupportTree(SkinPoint[] skin,Rig rig)
        {
            points=skin.Select((p,i)=>new Point{Skin=p,Index=i,Rest=p.Evaluate(rig.Rest),Coefficients=p.Weights.GroupBy(w=>w.Joint).Select(g=>new Coefficient{
                Joint=g.Key,X=g.Sum(w=>(double)w.Local.X*w.Weight),Y=g.Sum(w=>(double)w.Local.Y*w.Weight),Z=g.Sum(w=>(double)w.Local.Z*w.Weight),W=g.Sum(w=>(double)w.Weight)
            }).ToArray()}).ToArray();
            root=Build(0,points.Length);
        }
        private Node Build(int start,int count)
        {
            var bounds=new Dictionary<int,Bound>();
            Vector3 minimum=new Vector3(float.MaxValue),maximum=new Vector3(float.MinValue);
            for(int i=start;i<start+count;i++)
            {
                minimum=Vector3.Min(minimum,points[i].Rest);maximum=Vector3.Max(maximum,points[i].Rest);
                foreach(var c in points[i].Coefficients)
                {
                    if(!bounds.TryGetValue(c.Joint,out Bound b)){b=new Bound{Joint=c.Joint};bounds.Add(c.Joint,b);}
                    b.Seen++;
                    for(int axis=0;axis<4;axis++){double value=c.At(axis);b.Minimum[axis]=Math.Min(b.Minimum[axis],value);b.Maximum[axis]=Math.Max(b.Maximum[axis],value);}
                }
            }
            foreach(var b in bounds.Values)if(b.Seen<count)for(int axis=0;axis<4;axis++)
            {b.Minimum[axis]=Math.Min(0,b.Minimum[axis]);b.Maximum[axis]=Math.Max(0,b.Maximum[axis]);}
            var node=new Node{Start=start,Count=count,Bounds=bounds.Values.ToArray()};
            if(count<=16)return node;
            Vector3 size=maximum-minimum;int dimension=size.X>=size.Y && size.X>=size.Z?0:size.Y>=size.Z?1:2;
            Array.Sort(points,start,count,Comparer<Point>.Create((a,b)=>{
                float x=dimension==0?a.Rest.X:dimension==1?a.Rest.Y:a.Rest.Z,y=dimension==0?b.Rest.X:dimension==1?b.Rest.Y:b.Rest.Z;
                int order=x.CompareTo(y);return order==0?a.Index.CompareTo(b.Index):order;
            }));
            int half=count/2;node.Left=Build(start,half);node.Right=Build(start+half,count-half);return node;
        }
        private static double LowerBound(Node node,double[] plane)
        {
            double lower=0,magnitude=0;
            foreach(var b in node.Bounds)for(int axis=0;axis<4;axis++)
            {
                double c=plane[b.Joint*4+axis];
                lower+=c*(c>=0?b.Minimum[axis]:b.Maximum[axis]);
                magnitude+=Math.Abs(c)*Math.Max(Math.Abs(b.Minimum[axis]),Math.Abs(b.Maximum[axis]));
            }
            // Float skinning roundoff, including cancellation, can only loosen
            // this bound. Exact leaf values and original-index ties are retained.
            return lower-magnitude*0.000002-0.000001;
        }
        internal SkinPoint Lowest(RigPose pose,out float height)
        {
            double[] plane=pose.GroundPlaneCoefficients();float best=float.MaxValue;int index=int.MaxValue;SkinPoint result=null;
            Search(root,LowerBound(root,plane),pose,plane,ref best,ref index,ref result);height=best;return result;
        }
        private void Search(Node node,double lower,RigPose pose,double[] plane,ref float best,ref int index,ref SkinPoint result)
        {
            if(lower>best)return;
            if(node.Left==null)
            {
                for(int i=node.Start;i<node.Start+node.Count;i++)
                {
                    float h=Vector3.Dot(points[i].Skin.Evaluate(pose),pose.Rig.GroundNormal);
                    if(h<best || (h==best && points[i].Index<index)){best=h;index=points[i].Index;result=points[i].Skin;}
                }
                return;
            }
            double a=LowerBound(node.Left,plane),b=LowerBound(node.Right,plane);
            if(a<=b){Search(node.Left,a,pose,plane,ref best,ref index,ref result);Search(node.Right,b,pose,plane,ref best,ref index,ref result);}
            else{Search(node.Right,b,pose,plane,ref best,ref index,ref result);Search(node.Left,a,pose,plane,ref best,ref index,ref result);}
        }
    }
}
