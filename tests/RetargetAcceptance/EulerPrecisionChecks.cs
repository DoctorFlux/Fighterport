using BrawlCrate.Studio.Retargeting;
using System;
using System.Numerics;
using System.Reflection;

internal static partial class Program
{
    private static void EulerPrecisionChecks()
    {
        var near=typeof(BrawlRetargeter).GetMethod("EulerNear",BindingFlags.NonPublic|BindingFlags.Static);
        foreach(double sign in new[]{-1d,1d})
        foreach(double pitch in new[]{89d,89.95,89.99,89.999,90d,90.001,90.01,90.05,91d})
        foreach(double turn in new[]{-720d,0d,720d})
        {
            var expected=new Vector3((float)(23+turn),(float)(pitch*sign),(float)(137+turn));
            // Construct the independent native Rz*Ry*Rx quaternion in double,
            // retaining only the final authentic float quaternion transport.
            double x=expected.X*Math.PI/360,y=expected.Y*Math.PI/360,z=expected.Z*Math.PI/360;
            double sx=Math.Sin(x),cx=Math.Cos(x),sy=Math.Sin(y),cy=Math.Cos(y),sz=Math.Sin(z),cz=Math.Cos(z);
            var q=new Quaternion((float)(sx*cy*cz-cx*sy*sz),(float)(cx*sy*cz+sx*cy*sz),
                (float)(cx*cy*sz-sx*sy*cz),(float)(cx*cy*cz+sx*sy*sz));
            var actual=(Vector3)near.Invoke(null,new object[]{q,(Vector3?)expected});
            var roundTrip=BrawlRetargeter.Rotation(new BrawlLib.Internal.Vector3(actual.X,actual.Y,actual.Z));
            var delta=Quaternion.Normalize(Quaternion.Inverse(q)*roundTrip);
            double angle=2*Math.Atan2(Math.Sqrt((double)delta.X*delta.X+(double)delta.Y*delta.Y+(double)delta.Z*delta.Z),Math.Abs(delta.W))*180/Math.PI;
            Check(angle<.0003,"Double polar extraction retains native orientation at and across both XYZ poles without removing full-turn branches",new{sign,pitch,turn,angle});
            // Float quaternion uncertainty is amplified by 1/cos(pitch) in
            // Euler components. The orientation bound above stays strict;
            // this separate check rejects a discrete family/whole-turn jump.
            double chartBound=Math.Abs(pitch-90)<1e-9?.001:Math.Max(.001,1e-6/Math.Abs(Math.Cos(pitch*Math.PI/180))*180/Math.PI);
            Check(Vector3.Distance(actual,expected)<chartBound,"Continuous pole gauge retains the nearest whole-turn family within the float quaternion chart's conditioning bound",new{sign,pitch,turn,actual,expected,chartBound});
        }
    }
}
