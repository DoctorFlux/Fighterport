using BrawlCrate.Studio.Retargeting;
using System;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Web.Script.Serialization;

internal static partial class Program
{
    private static int LocalPrecisionChecks(string output)
    {
        Directory.CreateDirectory(output);
        EulerPrecisionChecks();
        FromToPrecisionChecks();
        RotationSpanPrecisionChecks();
        AdaptiveRotationPrecisionChecks();
        CorrectionTrajectoryChecks();
        CorrectionSupportChecks();
        CoupledOrientationChecks();
        SourceSnapshotExtentChecks(output);
        foreach(bool permuted in new[]{false,true})foreach(float units in new[]{.7f,1.9f})
        {
            var source=CoordinateFixture("opaque-source-",1.3f,.41f,5,Quaternion.Normalize(Quaternion.CreateFromAxisAngle(Vector3.UnitZ,.27f)*Quaternion.CreateFromAxisAngle(Vector3.UnitX,-.13f)));
            var target=CoordinateFixture("opaque-target-",units,-.37f,9,Quaternion.Normalize(Quaternion.CreateFromAxisAngle(Vector3.UnitY,-.61f)*Quaternion.CreateFromAxisAngle(Vector3.UnitZ,-.19f)),permuted:permuted);
            var plan=new HumanoidRetargeter(source.Rig,target.Rig);
            var independent=Quaternion.Normalize(target.WorldFrame*Quaternion.Inverse(source.WorldFrame));
            int sourceHead=source.Rig.Roles["head"],targetHead=target.Rig.Roles["head"],neck=source.Rig.Roles["neck"];
            foreach(bool atBind in new[]{false,true})
            {
                Quaternion? first=null;double maximum=0;
                var constant=atBind?source.Rig.Joints[sourceHead].Bind.Rotation:
                    Quaternion.Normalize(Quaternion.CreateFromAxisAngle(Vector3.Normalize(new Vector3(1,2,-3)),.37f)*source.Rig.Joints[sourceHead].Bind.Rotation);
                for(int frame=0;frame<101;frame++)
                {
                    var local=source.Rig.Joints.Select(j=>j.Bind).ToArray();
                    local[sourceHead].Rotation=constant;
                    local[neck].Rotation=Quaternion.Normalize(Quaternion.CreateFromAxisAngle(Vector3.Normalize(new Vector3(2,-1,3)),frame*.071f)*local[neck].Rotation);
                    var pose=new RigPose(source.Rig,local);var result=plan.Evaluate(pose,false).Pose;
                    var actual=result.Local[targetHead].Rotation;
                    if(!first.HasValue)first=actual;
                    Check(actual==first.Value,"A constant authored local rotation stays bit-stable under changing ancestor motion on renamed/permuted unequal native rigs",new{permuted,units,atBind,frame});
                    var desired=Quaternion.Normalize(independent*pose.Orientation[sourceHead]*Quaternion.Inverse(source.BindWorld[sourceHead])*Quaternion.Inverse(independent)*target.BindWorld[targetHead]);
                    maximum=Math.Max(maximum,CoordinateAngle(result.Orientation[targetHead],desired));
                }
                Check(maximum<.001,"Factorized local FK independently matches the original world-bind-relative equation",new{permuted,units,atBind,maximum});
                if(atBind)Check(first.Value==target.Rig.Joints[targetHead].Bind.Rotation,"Authentic target bind rotation remains exact when the corresponding source local channel stays at bind");
            }
        }
        var fit=typeof(BrawlRetargeter).GetMethod("FitTangents",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Static);
        foreach(float value in new[]{0f,-0f,1f,-1f,.1f,-179.9185f,720f,1e20f})
        foreach(int frames in new[]{2,9,44})
        {
            var samples=Enumerable.Range(0,(frames-1)*8+1).Select(_=>new[]{new[]{value}}).ToList();
            var tangents=(float[])fit.Invoke(null,new object[]{samples,0,0,frames-1,8});
            Check(tangents.All(v=>BitConverter.ToInt32(BitConverter.GetBytes(v),0)==0),"A genuinely constant sampled function bakes only exact zero derivatives without cancellation noise",new{value,frames});
        }
        // Equal integer endpoints alone must never erase fractional motion.
        var pulse=Enumerable.Range(0,9).Select(i=>new[]{new[]{i==4?1f:0f}}).ToList();
        var pulseTangents=(float[])fit.Invoke(null,new object[]{pulse,0,0,1,8});
        Check(pulseTangents.Any(v=>v!=0),"Constant cleanup does not erase a fractional excursion between equal integer endpoints");
        File.WriteAllText(Path.Combine(output,"local-precision.json"),new JavaScriptSerializer().Serialize(new{Passed=true,Checks,NoCharacterActionNameRules=true,NoNativeAssets=true,OriginalWorldEquationOracle=true}));
        return 0;
    }
}
