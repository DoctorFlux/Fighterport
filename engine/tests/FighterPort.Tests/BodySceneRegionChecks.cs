using System.Numerics;
using FighterPort.Core;

// Area and Cartesian deformation expectations are authored independently of
// the quadrature selector. These checks exercise its >27-point branch without
// treating retained points as a proof of posed whole-mesh coverage.
internal static class BodySceneRegionChecks
{
    sealed record Fixture(Scene Scene,ControlRigSetup Rig,int Body);

    public static void Run(Action<bool,string> check)
    {
        var coarse=Grid(8,6);var fine=Grid(16,12);
        string before=RetargetDependencies.Hash(new{Coarse=coarse,Fine=fine});
        var rig=RigDescriptor.Prepare(coarse.Scene,coarse.Rig);
        var regions=BodySceneRegions.Prepare(rig,coarse.Body);
        var dense=BodySceneRegions.Prepare(RigDescriptor.Prepare(fine.Scene,fine.Rig),fine.Body);
        // The independent rectangle has width4, depth6 and area24. Repeated
        // slots on the first body descendant total .375; the other receives
        // .125, and the actor receives .5. No rest length enters these areas.
        check(regions.Length==3&&Area(regions,0,12)&&Area(regions,3,9)&&Area(regions,4,3)&&
            Math.Abs(regions.Sum(r=>r.Area)-24)<.0002,
            "Native direct-influence regions partition the independent 24-unit rectangle by actual .5/.375/.125 weights, including repeated skin slots");
        check(dense.Length==3&&Area(dense,0,12)&&Area(dense,3,9)&&Area(dense,4,3)&&
            regions.All(r=>Math.Abs(r.Area-dense.Single(s=>s.Bone==r.Bone).Area)<.0002),
            "Quadrupling triangle density preserves actual weighted surface area rather than replacing it with vertex-count mass");
        check(regions.All(r=>r.OriginalPointCount==63&&r.Sensors.Length is >0 and <=27&&r.Sensors.Length<r.OriginalPointCount)&&
            dense.All(r=>r.OriginalPointCount==221&&r.Sensors.Length is >0 and <=27&&r.Sensors.Length<r.OriginalPointCount),
            "Both 63-point and 221-point native regions exercise bounded quadrature and report their unreduced point counts honestly");
        check(regions.Concat(dense).All(r=>double.IsFinite(r.Area)&&r.Area>0&&float.IsFinite(r.Radius)&&r.Radius>0&&
            r.Sensors.All(s=>double.IsFinite(s.Area)&&s.Area>=0)&&Math.Abs(r.Sensors.Sum(s=>s.Area)-r.Area)<.0000001),
            "Every retained native region conserves all original triangle-area mass with finite positive extent and nonnegative quadrature weights");
        check(regions.Concat(dense).All(r=>r.OriginalSensors.Length==r.OriginalPointCount&&
            r.OriginalSensors.Select(s=>(s.Point.Mesh,s.Point.Vertex)).Distinct().Count()==r.OriginalPointCount&&
            Math.Abs(r.OriginalSensors.Sum(s=>s.Area)-r.Area)<.0000001&&
            r.OriginalSensors.All(s=>double.IsFinite(s.Area)&&s.Area>=0&&s.BodyInfluence==.5f)),
            "Unpooled source sensors retain every original weighted material vertex and conserve its own rest-area measure before target quadrature pooling");
        check(OriginalIdentities(coarse.Scene,regions.Select(r=>r with{Sensors=r.OriginalSensors}).ToArray())&&
            OriginalIdentities(fine.Scene,dense.Select(r=>r with{Sensors=r.OriginalSensors}).ToArray()),
            "Every unpooled source sensor retains its actual original mesh/vertex, bind position and full native joint-weight slots");
        check(Math.Abs(regions.Single(r=>r.Bone==3).OriginalSensors.Single(s=>s.Point.Vertex==0).Area-.03125)<.0000001&&
            Math.Abs(regions.Single(r=>r.Bone==3).OriginalSensors.Single(s=>s.Point.Vertex==21).Area-.1875)<.0000001,
            "Original source corner and interior masses equal independent one-triangle and six-triangle rectangle areas times the actual .375 direct bone weight");
        check(Math.Abs(regions.Sum(r=>r.OriginalSensors.Where(s=>s.Point.Position.Z==-3).Sum(s=>s.Area))-2)<.000002,
            "A narrow source boundary-row query receives the independent two-unit rest-area mass rather than the larger area pooled onto a retained extremum");
        check(OriginalIdentities(coarse.Scene,regions)&&OriginalIdentities(fine.Scene,dense),
            "Every quadrature point retains a unique real mesh/vertex identity and its original bind position, joints and weights");
        check(CoversRectangle(coarse.Scene,regions)&&CoversRectangle(fine.Scene,dense),
            "Reported rest omission bounds cover every original rectangle vertex and remain within its independently known sqrt(52) diagonal");
        check(regions.SelectMany(r=>r.Sensors).All(s=>Math.Abs(s.BodyInfluence-.5f)<.0000001f),
            "Mixed actor/body influence is exactly .5 across arbitrary existing descendant bones, including repeated native slots");

        // Native actor rest is scale(2,3,1), then a +90-degree Z turn.
        // Local body displacement(2,-1,3) therefore becomes world(3,4,3).
        // Only half each vertex's skin follows it: expected delta(1.5,2,1.5).
        var body=coarse.Scene.Bones[coarse.Body].Rest;
        var moved=AnimationService.Evaluate(coarse.Scene,null,0,new Dictionary<int,Transform>
        {[coarse.Body]=body with{Translation=body.Translation+new Vector3(2,-1,3)}});
        var expected=new Vector3(1.5f,2,1.5f);
        check(regions.SelectMany(r=>r.Sensors).All(s=>
            Vector3.Distance(ContactGeometry.Point(s.Point,moved),s.Point.Position+expected)<.00002f),
            "Retained mixed skin responds by the independent Cartesian half-translation through a rotated, nonuniformly scaled native parent");

        var positions=coarse.Scene.Meshes.Select((m,mi)=>m.Vertices.Select((v,vi)=>
            v.Position+new Vector3((vi%9)*.03125f,(vi/9)*.125f,.5f)).ToArray()).ToArray();
        var morphed=moved with{GeometryPositions=positions};
        check(regions.SelectMany(r=>r.Sensors).All(s=>
            Vector3.Distance(ContactGeometry.Point(s.Point,morphed),positions[s.Point.Mesh][s.Point.Vertex]+expected)<.00002f),
            "Prepared geometry positions follow every retained original mesh/vertex identity through actual morph displacement and mixed native skinning");
        check(regions.SelectMany(r=>r.Sensors).Any(s=>
            Vector3.Distance(ContactGeometry.Point(s.Point,morphed),ContactGeometry.Point(s.Point,moved))>.25f),
            "The morph-ID check observes a real geometry change rather than merely evaluating unchanged cached bind points");
        check(regions.SelectMany(r=>r.OriginalSensors).All(s=>
            Vector3.Distance(ContactGeometry.Point(s.Point,morphed),positions[s.Point.Mesh][s.Point.Vertex]+expected)<.00002f),
            "Every unpooled source material sample follows prepared morph positions and the independent mixed-body Cartesian displacement without modifying its rest-area mass");

        int[] order=[0,2,4,1,3,5];var permutation=Permute(coarse,order);
        var permuted=BodySceneRegions.Prepare(RigDescriptor.Prepare(permutation.Scene,permutation.Rig),permutation.Body);
        var reverse=order.Select((old,index)=>(old,index)).ToDictionary(p=>p.old,p=>p.index);
        check(regions.All(r=>Equivalent(r,permuted.Single(p=>p.Bone==reverse[r.Bone]))),
            "Supplied renamed bindings and a valid native sibling/storage permutation preserve selected geometry, area, body response and approximation bounds");
        check(regions.All(r=>r.OriginalSensors.All(s=>permuted.Single(p=>p.Bone==reverse[r.Bone]).OriginalSensors.Any(t=>
            s.Point.Mesh==t.Point.Mesh&&s.Point.Vertex==t.Point.Vertex&&s.Point.Position==t.Point.Position&&
            Math.Abs(s.Area-t.Area)<.0000001&&s.BodyInfluence==t.BodyInfluence))),
            "Valid renamed native-bone storage permutation preserves each unpooled source material identity, rest-area mass and body influence");
        var isolatedScene=coarse.Scene with{Meshes=[coarse.Scene.Meshes[0] with
        {Vertices=coarse.Scene.Meshes[0].Vertices.Append(coarse.Scene.Meshes[0].Vertices[0] with{Position=new(25,25,25)}).ToArray()}]};
        var isolated=BodySceneRegions.Prepare(RigDescriptor.Prepare(isolatedScene,coarse.Rig),coarse.Body);
        check(isolated.All(r=>r.OriginalPointCount==64&&r.OriginalSensors.Length==64&&
            r.OriginalSensors.Single(s=>s.Point.Vertex==63).Area==0&&Math.Abs(r.Area-regions.Single(p=>p.Bone==r.Bone).Area)<.0000001),
            "An unreferenced original skinned vertex is retained with honest zero rest-area mass and does not fabricate source influence area");
        var repeated=BodySceneRegions.Prepare(rig,coarse.Body);
        check(regions.All(r=>Equivalent(r,repeated.Single(p=>p.Bone==r.Bone))),
            "Repeated offline preparation selects the same material geometry and mass without playback-history seeds");
        check(before==RetargetDependencies.Hash(new{Coarse=coarse,Fine=fine}),
            "Region preparation, posed/morphed sampling and permutation preserve original rest hierarchy, skin weights, geometry and reviewed setup data");
    }

    static bool Area(BodySceneRegion[] regions,int bone,double expected)=>
        Math.Abs(regions.Single(r=>r.Bone==bone).Area-expected)<.0002;

    static bool OriginalIdentities(Scene scene,BodySceneRegion[] regions)=>regions.All(r=>
        r.Sensors.Select(s=>(s.Point.Mesh,s.Point.Vertex)).Distinct().Count()==r.Sensors.Length&&r.Sensors.All(s=>
        {
            var p=s.Point;if(p.Mesh<0||p.Mesh>=scene.Meshes.Length||p.Vertex<0||p.Vertex>=scene.Meshes[p.Mesh].Vertices.Length)return false;
            var v=scene.Meshes[p.Mesh].Vertices[p.Vertex];return p.Position==v.Position&&p.Joints==v.Joints&&p.Weights==v.Weights;
        }));

    static bool CoversRectangle(Scene scene,BodySceneRegion[] regions)=>regions.All(r=>
    {
        if(!float.IsFinite(r.MaximumOmittedRestDistance)||r.MaximumOmittedRestDistance<=0||r.MaximumOmittedRestDistance>Math.Sqrt(52)+.00002)return false;
        // This is an independent coverage property of the material vertices,
        // not an expectation built from the selector's own assignments.
        return scene.Meshes.SelectMany(m=>m.Vertices).All(v=>
            r.Sensors.Any(s=>Vector3.Distance(v.Position,s.Point.Position)<=r.MaximumOmittedRestDistance+.00002f));
    });

    static bool Equivalent(BodySceneRegion a,BodySceneRegion b)
    {
        if(a.OriginalPointCount!=b.OriginalPointCount||a.Sensors.Length!=b.Sensors.Length||Math.Abs(a.Area-b.Area)>.0000001||
            Math.Abs(a.Radius-b.Radius)>.00002||Math.Abs(a.MaximumOmittedRestDistance-b.MaximumOmittedRestDistance)>.00002)return false;
        return a.Sensors.All(s=>b.Sensors.Any(t=>s.Point.Position==t.Point.Position&&
            Math.Abs(s.Area-t.Area)<.0000001&&Math.Abs(s.BodyInfluence-t.BodyInfluence)<.0000001f));
    }

    static Fixture Grid(int columns,int rows)
    {
        var bones=new[]{
            new Bone("unfamiliar_actor",-1,new(new(2,3,1),new(0,0,90),Vector3.Zero)),
            new Bone("unrelated_branch",0,Transform.Identity with{Translation=new(.3f,.2f,1)}),
            new Bone("native_body",0,Transform.Identity with{Translation=new(0,3,0)}),
            new Bone("native_surface_a",2,Transform.Identity with{Translation=new(.5f,-1,0)}),
            new Bone("native_surface_b",2,Transform.Identity with{Translation=new(-.5f,-1,.5f)}),
            new Bone("unweighted_helper",3,Transform.Identity with{Translation=new(0,.25f,0)})};
        var vertices=new List<Vertex>();var indices=new List<uint>();
        for(int z=0;z<=rows;z++)for(int x=0;x<=columns;x++)
            vertices.Add(new(new(-2+4f*x/columns,1,-3+6f*z/rows),Vector3.UnitY,Vector2.Zero,
                new(3,0,4,3),new(.125f,.5f,.125f,.25f),Vector4.One));
        for(int z=0;z<rows;z++)for(int x=0;x<columns;x++)
        {
            uint a=(uint)(z*(columns+1)+x),b=a+1,c=a+(uint)columns+1,d=c+1;
            indices.AddRange([a,c,b,b,c,d]);
        }
        var scene=new Scene("Independent weighted four-by-six rectangle",bones,[new("Native retained surface",vertices.ToArray(),indices.ToArray())]);
        return new(scene,Setup(scene,2,[0,1,2,3,4,5]),2);
    }

    static ControlRigSetup Setup(Scene scene,int body,int[] roles)=>new(Guid.NewGuid(),AnimationControls.Fingerprint(scene),[],[])
    {
        BodyBone=body,Axes=new(Vector3.UnitX,Vector3.UnitY,Vector3.UnitZ),Coordinates=new(1,"Independent Cartesian rectangle"),
        BoneRoles=new[]{"RootMotion","Ancillary","Hips","Surface.A","Surface.B","Surface.Helper"}
            .Select((role,index)=>new RigMapping(role,roles[index],"Supplied native correspondence",true)).ToArray()
    };

    static Fixture Permute(Fixture fixture,int[] order)
    {
        var inverse=order.Select((old,index)=>(old,index)).ToDictionary(p=>p.old,p=>p.index);
        var scene=fixture.Scene with
        {
            SkeletonRevision=Guid.NewGuid(),
            Bones=order.Select((old,index)=>fixture.Scene.Bones[old] with{Name="opaque_"+index,
                Parent=fixture.Scene.Bones[old].Parent is var parent&&parent>=0?inverse[parent]:-1}).ToArray(),
            Meshes=fixture.Scene.Meshes.Select(m=>m with{Vertices=m.Vertices.Select(v=>v with
            {Joints=new(inverse[(int)v.Joints.X],inverse[(int)v.Joints.Y],inverse[(int)v.Joints.Z],inverse[(int)v.Joints.W])}).ToArray()}).ToArray()
        };
        return new(scene,Setup(scene,inverse[fixture.Body],Enumerable.Range(0,6).Select(i=>inverse[i]).ToArray()),inverse[fixture.Body]);
    }
}
