using System.Numerics;
using FighterPort.Core;

internal static class BodySurfaceReachChecks
{
    sealed record Fixture(Scene Scene,ControlRigSetup Setup,RigDescriptor Rig);
    public static void Run(Action<bool,string> check)
    {
        var band=new NativeTrajectoryPreconditioner(3,3);double[][] rows=[[1,2,0],[0,1,-1],[1,0,2]];
        var normal=new double[3,3];for(int a=0;a<3;a++)for(int b=0;b<3;b++)normal[a,b]=rows.Sum(r=>r[a]*r[b]);
        band.Add([0,1,2],[-.1,.8,.3],normal);band.Add([1,2],[.4,.6],normal);band.Add([0,1],[-1,1]);
        var expectedMatrix=new double[9,9];for(int i=0;i<9;i++)expectedMatrix[i,i]=.2;
        void Group(int[] controls,double[] weights,double[][] vectors)
        {
            foreach(var vector in vectors)
            {
                var row=new double[9];for(int k=0;k<controls.Length;k++)for(int v=0;v<3;v++)row[3*controls[k]+v]=weights[k]*vector[v];
                for(int a=0;a<9;a++)for(int b=0;b<9;b++)expectedMatrix[a,b]+=row[a]*row[b];
            }
        }
        Group([0],[-.1],rows);Group([1,2],[.8,.3],rows);Group([1,2],[.4,.6],rows);Group([0,1],[-1,1],[[1,0,0],[0,1,0],[0,0,1]]);
        double[] expectedVector=[.1,.2,-.3,.4,-.5,.6,.7,.8,-.9];var rhs=Enumerable.Range(0,9).Select(a=>Enumerable.Range(0,9).Sum(b=>expectedMatrix[a,b]*expectedVector[b])).ToArray();
        check(band.Factor(.2,default)&&band.Solve(rhs).Zip(expectedVector).Max(p=>Math.Abs(p.First-p.Second))<1e-12,
            "Temporal band factor/triangular solves recover an independent prescribed vector through a dense Cartesian Gram oracle with coupled pose components and neighboring control groups");
        var source=Make("opaque-a",.5f);var large=Make("opaque-b",1);var small=Make("opaque-c",.25f);
        string original=RetargetDependencies.Hash(new{Source=source.Scene,Target=large.Scene});
        var observed=Observe(source,large);var head=observed.Single(g=>g.TargetAnchor==2);
        // Original octahedra have tips at y=2.5/3 and wrist lower tip at3.9.
        // Equal surface separation1.4 requests wrist centre4.5, but this
        // original two-unit chain starts at2: it cannot reach past4.
        check(Vector3.Distance(head.RequestedLocal,new(0,2.5f,0))<.00001f&&
            Vector3.Distance(head.DesiredLocal,new(0,2,0))<.00001f&&head.ReachAdapted&&head.RequestedReach==2.5f&&head.AvailableReach==2,
            "Changed body volume retains the independently known 4.5-unit request but plans its soft centre goal at the actual four-unit workspace boundary without growing a bone");
        check(Vector3.Distance(Observe(large,source).Single(g=>g.TargetAnchor==2).DesiredLocal,new(0,1.5f,0))<.00001f&&
              Observe(source,small).Single(g=>g.TargetAnchor==2).DesiredLocal.Y<head.DesiredLocal.Y,
            "Source-relative surface separation transfers independently in both body-volume directions without changing any rest length");
        var same=Observe(source,source);check(same.All(g=>Vector3.Distance(g.Endpoint.Evaluate(source.Rig.Rest)-g.Anchor.Evaluate(source.Rig.Rest),g.DesiredLocal)<.00001f),
            "Same-rig source surface observations reproduce actual native material relationships at rest");
        var projected=BodySurfaceReachGoals.PlanReach(new(8,10,3),new(1,2,3),new(1,2,0),5);
        // displacement=(6,6,0); the sphere point is(2,4,3)+5/sqrt2*(1,1,0).
        var expectedPlan=new Vector3((float)(2+5/Math.Sqrt(2)),(float)(4+5/Math.Sqrt(2)),3);
        check(projected.Adapted&&Vector3.Distance(projected.Desired,expectedPlan)<1e-6&&Math.Abs(projected.Requested-Math.Sqrt(72))<1e-6,
            "An independent oblique Cartesian workspace oracle includes both the native chain root and full material-centre offset rather than clipping anchor distance");
        var rotated=BodySurfaceReachGoals.PlanReach(new Vector3(-10,8,3)*100,new Vector3(-2,1,3)*100,new Vector3(-2,1,0)*100,500);
        check(Vector3.Distance(rotated.Desired/100,new(-expectedPlan.Y,expectedPlan.X,expectedPlan.Z))<1e-6,
            "Soft workspace planning commutes with a declared hundred-unit and quarter-turn coordinate change");
        var reachable=BodySurfaceReachGoals.PlanReach(new(2,4,4),new(1,2,3),new(1,2,0),5);
        check(!reachable.Adapted&&reachable.Desired==new Vector3(2,4,4)&&reachable.Requested==1,
            "A reachable material request remains bit-identical and is never pushed to full extension");
        bool invalid=false;try{BodySurfaceReachGoals.PlanReach(Vector3.Zero,Vector3.Zero,Vector3.Zero,0);}catch(NotSupportedException){invalid=true;}
        check(invalid,"A zero native path span reports unsupported reach geometry without normalizing or stretching the skeleton");
        var changed=Make("unrelated-name",1,100);var metric=Observe(source,changed).Single(g=>g.TargetAnchor==2);
        check(Vector3.Distance(metric.DesiredLocal/100,head.DesiredLocal)<.00002f,
            "Surface goals commute with declared file-unit changes and do not require matching native names");
        var permuted=Permute(large);var permutation=Observe(source,permuted).Single(g=>g.TargetAnchor==permuted.Rig.Binding.Roles["Volume"]);
        check(permutation.DesiredLocal==head.DesiredLocal,"Reviewed bindings produce exactly equivalent body-surface goals after unrelated names and a valid native branch-storage permutation");
        var regions=BodySceneRegions.Prepare(large.Rig,1);var points=regions.Single(r=>r.Bone==2).OriginalSensors.Select(s=>s.Point).ToArray();
        var moment=new NativeSurfaceMoment(large.Rig,points);var edits=large.Scene.Bones.Select(b=>b.Rest).ToArray();edits[1]=edits[1] with{Rotation=new(13,27,-11),Translation=new(.3f,-.2f,.4f)};edits[2]=edits[2] with{Rotation=new(-17,23,5)};
        EvaluatedPose Pose(Transform[] local)=>AnimationService.Evaluate(large.Scene,null,0,local.Select((t,i)=>(t,i)).ToDictionary(p=>p.i,p=>p.t));
        var pose=Pose(edits);var expected=points.Select(p=>ContactGeometry.Point(p,pose)).Aggregate(Vector3.Zero,(a,b)=>a+b)/points.Length;
        check(Vector3.Distance(expected,moment.Evaluate(pose))<.00001f,"Native surface first moments equal actual full material skinning under independently rotated ancestors");
        var mixed=points.Select(p=>p with{Joints=new(2,1,0,0),Weights=new(.6f,.4f,0,0)}).ToArray();var mixedMoment=new NativeSurfaceMoment(large.Rig,mixed);
        var mixedExpected=mixed.Select(p=>ContactGeometry.Point(p,pose)).Aggregate(Vector3.Zero,(a,b)=>a+b)/mixed.Length;
        check(Vector3.Distance(mixedExpected,mixedMoment.Evaluate(pose))<.00001f,"Averaging mixed native influences preserves their actual parent/head material response rather than assigning a centroid to one rigid bone");
        int[] rotations=[1,2,3,4,5];var objective=new NativeSceneObjective(large.Rig,1,rotations,[1,2,3,4,5],[5],[],edits,(_,d)=>new([],new double[0,d?.Variables??0]),default,surfaceGoals:observed);
        var x=new double[objective.Variables];x[4]=.04;x[8]=-.03;var evaluated=objective.Evaluate(x,true);double maximum=0;const double h=.004;
        for(int c=0;c<x.Length;c++)
        {
            var plus=x.ToArray();var minus=x.ToArray();plus[c]+=h;minus[c]-=h;
            var a=objective.Evaluate(plus,false).Residual;var b=objective.Evaluate(minus,false).Residual;
            for(int r=a.Length-3*observed.Length;r<a.Length;r++)maximum=Math.Max(maximum,Math.Abs((a[r]-b[r])/(2*h)-evaluated.Jacobian[r,c]));
        }
        check(maximum<.0002,$"Surface-relative residual derivatives include moving body frames and both native skin moments; independent finite-difference maximum={maximum:G9}");
        double[] BodyRows(int[] variables)
        {
            var item=new NativeSceneObjective(large.Rig,1,variables,[1,2,3,4,5],[5],[],edits,(_,d)=>new([],new double[0,d?.Variables??0]),default);
            var parameters=new double[item.Variables];parameters[3]=.1;
            return item.Evaluate(parameters,false).Residual.Skip(item.Variables+15+9).Take(9).ToArray();
        }
        check(BodyRows([1,2,3,4,5]).SequenceEqual(BodyRows([1,3,4,5])),
            "The reviewed body-frame action mass is unchanged when compatible interior native joints are added to the editable variable set");
        check(RetargetDependencies.Hash(new{Source=source.Scene,Target=large.Scene})==original,"Body-surface preparation and objective evaluation preserve source/target native graph, rests and original material weights");
        var times=Enumerable.Range(0,65).Select(i=>i/64d).ToArray();var values=times.Select(t=>(float)Math.Pow(t,8)).ToArray();
        var initial=new Curve([new(0,0),new(1,1)]);var refined=PoseMotionReconstruction.RefineFractionalKnots(initial,times,values,.001f);
        double error=Enumerable.Range(0,200).Max(i=>Math.Abs(refined.Curve.Sample((i+.271828)/201)-Math.Pow((i+.271828)/201,8)));
        check(refined.Improved&&refined.Curve.Keys.Length>2&&error<.001,
            $"Fractional native keys represent independently authored high-curvature motion at withheld times while integer endpoints remain immutable; maximum={error:G9}");
        check(refined.Curve.Keys.Count(k=>k.Frame==Math.Truncate(k.Frame))==2&&refined.Curve.Keys.First().Value==0&&refined.Curve.Keys.Last().Value==1&&
              refined.Curve.Keys.All(k=>k.InTangent==k.OutTangent&&k.Frame*16==Math.Truncate(k.Frame*16)),
            "Fractional refinement uses exact dyadic native times and one shared finite derivative per knot without changing integer values");
    }
    static BodySurfaceReachObservation[] Observe(Fixture a,Fixture b)
    {
        var map=RetargetBoneMapping.Build(a.Scene,a.Setup,b.Scene,b.Setup,a.Rig.Binding.Roles,b.Rig.Binding.Roles,a.Rig.Basis,b.Rig.Basis);
        var reach=new BodyRelativeReachGoals(a.Rig,b.Rig,map);
        return new BodySurfaceReachGoals(a.Rig,b.Rig,map,reach.Bindings).Evaluate(a.Rig.Rest,b.Rig.Rest,_=>true);
    }
    static Fixture Permute(Fixture fixture)
    {
        int[] order=[0,1,3,4,5,2];var map=order.Select((old,index)=>(old,index)).ToDictionary(p=>p.old,p=>p.index);
        var scene=fixture.Scene with{Bones=order.Select((old,index)=>fixture.Scene.Bones[old] with{Name="unrelated-"+index,Parent=fixture.Scene.Bones[old].Parent<0?-1:map[fixture.Scene.Bones[old].Parent]}).ToArray(),
            Meshes=fixture.Scene.Meshes.Select(m=>m with{Vertices=m.Vertices.Select(v=>v with{Joints=new(map[(int)v.Joints.X],map[(int)v.Joints.Y],map[(int)v.Joints.Z],map[(int)v.Joints.W])}).ToArray()}).ToArray()};
        var setup=fixture.Setup with{SkeletonFingerprint=AnimationControls.Fingerprint(scene),BodyBone=map[fixture.Setup.BodyBone!.Value],
            BoneRoles=fixture.Setup.BoneRoles.Select(r=>r with{Bone=map[r.Bone]}).ToArray(),
            VirtualChains=(fixture.Setup.VirtualChains??[]).Select(c=>c with{Bones=c.Bones.Select(b=>map[b]).ToArray()}).ToArray()};
        return new(scene,setup,RigDescriptor.Prepare(scene,setup));
    }
    static Fixture Make(string prefix,float radius,float units=1)
    {
        var bones=new[]{new Bone(prefix+"0",-1,Transform.Identity),new Bone(prefix+"1",0,Transform.Identity),
            new Bone(prefix+"2",1,Transform.Identity with{Translation=new(0,2*units,0)}),new Bone(prefix+"3",1,Transform.Identity with{Translation=new(0,2*units,0)}),
            new Bone(prefix+"4",3,Transform.Identity with{Translation=new(0,units,0)}),new Bone(prefix+"5",4,Transform.Identity with{Translation=new(0,units,0)})};
        var vertices=new List<Vertex>();var indices=new List<uint>();
        foreach(var (bone,center,size) in new[]{(1,Vector3.Zero,.25f),(2,new Vector3(0,2,0),radius),(5,new Vector3(0,4,0),.1f)})
        {
            int start=vertices.Count;
            foreach(var d in new[]{Vector3.UnitX,-Vector3.UnitX,Vector3.UnitY,-Vector3.UnitY,Vector3.UnitZ,-Vector3.UnitZ})
                vertices.Add(new((center+d*size)*units,d,Vector2.Zero,new(bone,0,0,0),new(1,0,0,0),Vector4.One));
            foreach(int i in new[]{0,2,4,4,2,1,1,2,5,5,2,0,4,3,0,1,3,4,5,3,1,0,3,5})indices.Add((uint)(start+i));
        }
        var scene=new Scene(prefix,bones,[new("original-volume",vertices.ToArray(),indices.ToArray())]);
        var setup=new ControlRigSetup(Guid.NewGuid(),AnimationControls.Fingerprint(scene),[],[]){BodyBone=1,Axes=new(Vector3.UnitX,Vector3.UnitY,Vector3.UnitZ),Coordinates=new(units,"Authored independent units"),
            VirtualChains=[new("reviewed native path",[3,4,5],"Original Cartesian topology",true)],
            BoneRoles=[new("RootMotion",0,"Supplied",true),new("Hips",1,"Supplied",true),new("Volume",2,"Supplied",true),new("BranchRoot",3,"Supplied",true),new("BranchMid",4,"Supplied",true),new("BranchEnd",5,"Supplied",true)]};
        return new(scene,setup,RigDescriptor.Prepare(scene,setup));
    }
}
