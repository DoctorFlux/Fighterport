using BrawlCrate.Studio.Retargeting;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Web.Script.Serialization;

internal static partial class Program
{
    // Expectations frozen before the coordinate-up implementation: independently
    // authored world positions, bind axes and actor/body rotations. The oracle
    // never calls BodyFrame or uses the production Calibration to derive motion.
    private sealed class AxisFixture
    {
        internal Rig Rig;
        internal Quaternion[] BindWorld;
        internal Quaternion WorldFrame;
    }

    private static AxisFixture CoordinateFixture(string prefix,float units,float headForward,int axes,Quaternion frame,bool declared=true,bool permuted=false)
    {
        int[] parents={-1,0,1,2,3,4,3,6,7,3,9,10,1,12,13,14,1,16,17,18};
        Vector3[] points={Vector3.Zero,new Vector3(0,10,0),new Vector3(0,13,0),new Vector3(0,16,0),new Vector3(0,18,0),new Vector3(0,20,headForward),
            new Vector3(3,17,0),new Vector3(7,17,0),new Vector3(10,17,0),new Vector3(-3,17,0),new Vector3(-7,17,0),new Vector3(-10,17,0),
            new Vector3(1,9,0),new Vector3(1,5,0),new Vector3(1,1,0),new Vector3(1,1,2),new Vector3(-1,9,0),new Vector3(-1,5,0),new Vector3(-1,1,0),new Vector3(-1,1,2)};
        var roles=new Dictionary<string,int>{{"pelvis",1},{"spine",2},{"chest",3},{"neck",4},{"head",5},
            {"left.upperArm",6},{"left.lowerArm",7},{"left.hand",8},{"right.upperArm",9},{"right.lowerArm",10},{"right.hand",11},
            {"left.upperLeg",12},{"left.lowerLeg",13},{"left.foot",14},{"left.toe",15},{"right.upperLeg",16},{"right.lowerLeg",17},{"right.foot",18},{"right.toe",19}};
        var world=Enumerable.Range(0,points.Length).Select(i=>Quaternion.Normalize(frame*Quaternion.CreateFromAxisAngle(Vector3.Normalize(new Vector3(1+(i%2),2+(i%3),3)),axes*(i+1)*.071f))).ToArray();
        var positions=points.Select(p=>Vector3.Transform(p*units,frame)).ToArray();
        var joints=Enumerable.Range(0,points.Length).Select(i=>{
            int p=parents[i];Quaternion inverse=p<0?Quaternion.Identity:Quaternion.Inverse(world[p]);
            return new RigJoint(prefix+((i*37+11)%997),p,new JointTransform(Vector3.Transform(positions[i]-(p<0?Vector3.Zero:positions[p]),inverse),Quaternion.Normalize(inverse*world[i]),Vector3.One));
        }).ToArray();
        if(permuted)
        {
            var order=new List<int>();Action<int> visit=null;visit=p=>{foreach(int child in Enumerable.Range(0,joints.Length).Where(i=>parents[i]==p).Reverse()){order.Add(child);visit(child);}};visit(-1);
            var indices=order.Select((old,index)=>new{old,index}).ToDictionary(p=>p.old,p=>p.index);
            joints=order.Select(old=>new RigJoint(prefix+"anonymous_"+indices[old],parents[old]<0?-1:indices[parents[old]],joints[old].Bind)).ToArray();
            roles=roles.ToDictionary(p=>p.Key,p=>indices[p.Value]);world=order.Select(old=>world[old]).ToArray();
        }
        return new AxisFixture{Rig=new Rig(prefix,"independent reviewed axes",joints,roles,units,declared?(Vector3?)Vector3.Transform(Vector3.UnitY,frame):null),BindWorld=world,WorldFrame=frame};
    }

    private static RigPose CoordinatePose(AxisFixture fixture,Quaternion actor,Quaternion body,Vector3 travel)
    {
        var desired=fixture.BindWorld.Select((q,i)=>Quaternion.Normalize(fixture.WorldFrame*actor*(i==0?Quaternion.Identity:body)*Quaternion.Inverse(fixture.WorldFrame)*q)).ToArray();
        var local=fixture.Rig.Joints.Select((joint,i)=>{int p=joint.Parent;var bind=joint.Bind;return new JointTransform(bind.Translation+(p<0?Vector3.Transform(travel,fixture.WorldFrame):Vector3.Zero),Quaternion.Normalize((p<0?Quaternion.Identity:Quaternion.Inverse(desired[p]))*desired[i]),bind.Scale);}).ToArray();
        return new RigPose(fixture.Rig,local);
    }

    private static double CoordinateAngle(Quaternion a,Quaternion b)
    {
        a=Quaternion.Normalize(a);b=Quaternion.Normalize(b);if(Quaternion.Dot(a,b)<0)b=new Quaternion(-b.X,-b.Y,-b.Z,-b.W);
        double x=(double)a.X-b.X,y=(double)a.Y-b.Y,z=(double)a.Z-b.Z,w=(double)a.W-b.W;
        return 4*Math.Asin(Math.Min(1,Math.Sqrt(x*x+y*y+z*z+w*w)/2))*180/Math.PI;
    }

    private static int CoordinateFrameChecks(string output)
    {
        Directory.CreateDirectory(output);
        var evidence=new List<object>();
        var global=Quaternion.CreateFromAxisAngle(Vector3.Normalize(new Vector3(1,2,-3)),1.13f);
        foreach(var frame in new[]{Quaternion.Identity,global})foreach(float targetUnits in new[]{1f,2.3f})foreach(bool permuted in new[]{false,true})
        {
            var source=CoordinateFixture("unfamiliar-source",1,9,1,frame,true,permuted);
            var target=CoordinateFixture("unfamiliar-target",targetUnits,-4,3,frame,true,!permuted);
            var plan=new HumanoidRetargeter(source.Rig,target.Rig);
            Check(plan.CalibrationEvidence=="declared-coordinate-up/anatomical-heading-v1"&&CoordinateAngle(plan.Calibration,Quaternion.Identity)<.01,
                "Declared coordinate-up is independent of head/neck rest posture, units, local bone axes and sibling storage",new{targetUnits,permuted});
            var actor=Quaternion.Normalize(Quaternion.CreateFromAxisAngle(Vector3.UnitY,.47f)*Quaternion.CreateFromAxisAngle(Vector3.UnitZ,-.23f));
            foreach(var body in new[]{Quaternion.Identity,Quaternion.CreateFromAxisAngle(Vector3.UnitX,-.79f)})
            {
                var pose=CoordinatePose(source,actor,body,new Vector3(1.5f,.2f,-.7f));
                foreach(bool correct in new[]{false,true})
                {
                    var result=plan.Evaluate(pose,correct).Pose;double maximum=0;
                    foreach(var map in plan.Mapping)
                    {
                        int i=map.TargetIndex;var expected=Quaternion.Normalize(frame*actor*(plan.TargetMotionControls.Contains(i)?Quaternion.Identity:body)*Quaternion.Inverse(frame)*target.BindWorld[i]);
                        maximum=Math.Max(maximum,CoordinateAngle(result.Orientation[i],expected));
                    }
                    Check(maximum<.01,"Independent equal-anatomy world rotations survive non-Y-up covariance, unequal local axes and optional limb correction",new{correct,targetUnits,permuted,maximum});
                    foreach(int control in plan.TargetMotionControls)
                    {
                        var map=plan.Mapping.Single(m=>m.TargetIndex==control);var expectedDelta=pose.Local[map.SourceIndex].Translation-source.Rig.Joints[map.SourceIndex].Bind.Translation;
                        Check(Vector3.Distance(result.Local[control].Translation-target.Rig.Joints[control].Bind.Translation,expectedDelta)<.0002f&&Vector3.Distance(result.Local[control].Scale,target.Rig.Joints[control].Bind.Scale)<.00001f,
                            "Coordinate calibration cannot rescale gameplay translation or invent motion-control scale",new{targetUnits,correct,control});
                    }
                    evidence.Add(new{targetUnits,permuted,correct,maximum});
                }
            }
            var before=plan.Calibration;target.Rig.GroundNormal=Vector3.Normalize(new Vector3(.3f,1,-.2f));var changed=new HumanoidRetargeter(source.Rig,target.Rig);
            Check(CoordinateAngle(before,changed.Calibration)<.0001,"Changing a support-surface normal cannot change immutable adapter coordinate-up");
        }
        var unknownSource=CoordinateFixture("legacy-source",1,9,1,Quaternion.Identity,false);
        var unknownTarget=CoordinateFixture("legacy-target",1,-4,3,Quaternion.Identity,false);
        var legacy=new HumanoidRetargeter(unknownSource.Rig,unknownTarget.Rig);
        Check(legacy.CalibrationEvidence=="legacy-anatomical-rest-fallback",
            "Missing coordinate evidence keeps a labelled legacy fallback rather than claiming known world axes");
        // Original Next's no-evidence head-line frame is frozen analytically:
        // these fixtures have exactly10 units between pelvis/head in Y.
        var oldCalibration=Quaternion.CreateFromAxisAngle(Vector3.UnitX,(float)(Math.Atan2(-4,10)-Math.Atan2(9,10)));
        Check(CoordinateAngle(legacy.Calibration,oldCalibration)<.01,
            "The explicit no-evidence path retains the original Next anatomical-rest calibration math");
        var oldMotion=Quaternion.CreateFromAxisAngle(Vector3.UnitX,-.31f);
        var oldSource=CoordinatePose(unknownSource,Quaternion.Identity,oldMotion,Vector3.Zero);
        var oldResult=legacy.Evaluate(oldSource,false).Pose;double oldMaximum=0;
        foreach(var map in legacy.Mapping.Where(m=>!legacy.TargetMotionControls.Contains(m.TargetIndex)))
        {
            var expected=Quaternion.Normalize(oldCalibration*oldMotion*Quaternion.Inverse(oldCalibration)*unknownTarget.BindWorld[map.TargetIndex]);
            oldMaximum=Math.Max(oldMaximum,CoordinateAngle(oldResult.Orientation[map.TargetIndex],expected));
        }
        Check(oldMaximum<.01,"Legacy no-coordinate-evidence FK retains the independently frozen original Next world-delta formula",new{oldMaximum});
        foreach(var reviewed in new[]{Quaternion.Identity,global,Quaternion.CreateFromAxisAngle(Vector3.UnitY,(float)Math.PI)})
        {
            var frame=RigMath.ReviewedCoordinateFrame(Vector3.Transform(Vector3.UnitX,reviewed),Vector3.Transform(Vector3.UnitY,reviewed),Vector3.Transform(Vector3.UnitZ,reviewed));
            var sourceReview=new Rig("reviewed-source","explicit",unknownSource.Rig.Joints,unknownSource.Rig.Roles,1,coordinateFrame:frame);
            var targetReview=new Rig("reviewed-target","explicit",unknownTarget.Rig.Joints,unknownTarget.Rig.Roles,1,coordinateFrame:frame);
            var reviewedPlan=new HumanoidRetargeter(sourceReview,targetReview);
            Check(CoordinateAngle(RigMath.BodyFrame(sourceReview),reviewed)<.01&&CoordinateAngle(RigMath.BodyFrame(targetReview),reviewed)<.01&&
                reviewedPlan.CalibrationEvidence=="reviewed-coordinate-frame-v1"&&CoordinateAngle(reviewedPlan.Calibration,Quaternion.Identity)<.01,
                "Full reviewed axes, including signed/non-Y-up frames, take precedence over anatomical head/shoulder posture");
            var clone=new Rig("snapshot","explicit",targetReview.Joints,targetReview.Roles,targetReview.HeadRadius,targetReview.CoordinateUp,targetReview.CoordinateFrame);
            Check(clone.CoordinateFrame.HasValue&&CoordinateAngle(RigMath.BodyFrame(clone),reviewed)<.01,
                "An independent rig snapshot retains its complete reviewed coordinate frame and up evidence");
        }
        bool rejected=false;try{var invalid=new Rig("invalid","reviewed",unknownSource.Rig.Joints,unknownSource.Rig.Roles,1,new Vector3(float.NaN,1,0));}catch(InvalidDataException){rejected=true;}
        Check(rejected,"Invalid declared coordinate-up is rejected before retargeting");
        rejected=false;try{var parallel=new Rig("parallel","reviewed",unknownSource.Rig.Joints,unknownSource.Rig.Roles,1,Vector3.UnitX);RigMath.BodyFrame(parallel);}catch(InvalidDataException){rejected=true;}
        Check(rejected,"A declared up axis parallel to the reviewed lateral direction requires review, never a guessed head-line fallback");
        rejected=false;try{RigMath.ReviewedCoordinateFrame(Vector3.UnitX,Vector3.UnitY,-Vector3.UnitZ);}catch(NotSupportedException){rejected=true;}
        Check(rejected,"Left-handed reviewed axes are rejected explicitly rather than silently ignored or converted through a quaternion");
        rejected=false;try{RigMath.ReviewedCoordinateFrame(Vector3.UnitX,Vector3.UnitX,Vector3.UnitZ);}catch(InvalidDataException){rejected=true;}
        Check(rejected,"Nonorthogonal reviewed coordinate axes cannot prepare a retarget");
        rejected=false;try{var invalid=new Rig("contradictory","reviewed",unknownSource.Rig.Joints,unknownSource.Rig.Roles,1,Vector3.UnitY,Quaternion.CreateFromAxisAngle(Vector3.UnitZ,1));}catch(InvalidDataException){rejected=true;}
        Check(rejected,"Conflicting declared up and complete reviewed frame require review instead of one silently overriding the other");
        File.WriteAllText(Path.Combine(output,"coordinate-frames.json"),new JavaScriptSerializer{MaxJsonLength=16000000}.Serialize(new{Passed=true,Checks,Evidence=evidence,Expectation="independent analytic actor/body world rotations;0.01degree existing orientation tolerance"}));
        Console.WriteLine("PASS "+Checks.Count+" coordinate-frame checks");return 0;
    }
}
