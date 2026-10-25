using FighterPort.Core;
using System.Numerics;
using System.Text.Json;

internal static class BoundaryFloorRecoveryChecks
{
    sealed record Fixture(FighterProject Project,AnimationClip Source,AnimationClip Reference,BoundaryFitSettings Settings,int[] Ankles,int Body);
    // Frozen expectations before the solver: two unit leg segments, body height
    // 1+cos45. Original +45 and reference -45 knees put the ankle at Y=0.
    // Mid-recovery straight legs put it at -(1-cos45)=-.2928932188134524.
    // Eight native intervals give an integral midpoint knot and a .4 lift budget.
    const double AnalyticDip=.2928932188134524,PositionBound=.0002,ScaleBound=.00001,OrientationBound=.01;
    public static async Task RunAsync(string dir,Action<bool,string> check)
    {
        var basic=Create(1,1,0,0,false,false,1);var originalJson=JsonSerializer.Serialize(basic.Project,ProjectStore.Json);
        var without=Fit(basic,basic.Settings with{FloorSettings=null});var nativeMid=Evaluate(basic.Project.Scene,without.Clip,8);
        double mid=Point(basic.Project.Scene.Meshes[0].Vertices[0],nativeMid.Skin).Y;
        check(Math.Abs(mid+AnalyticDip)<.000003,"Independent analytic two-link fixture exposes the unfitted -.2928932188 native-unit midpoint dip");
        var fit=Fit(basic);var floor=fit.Receipt.FloorRecovery!;
        check(fit.Receipt.Version==BoundaryFitting.FloorVersion&&floor.Version==BoundaryFitting.FloorVersion&&floor.MaximumLift>AnalyticDip-.00002&&floor.MaximumLift<AnalyticDip+.0002&&Math.Abs(floor.LiftLimit-.4)<.000002,
            "One generic body-normal envelope clears the analytic dip within the frozen .4 two-link lift budget");
        Verify(basic,fit,check,"analytic");
        check(JsonSerializer.Serialize(basic.Project,ProjectStore.Json)==originalJson,"Floor preparation leaves complete scene, authentic weights, source curves and constraints immutable");
        check(floor.BaselinePolicy==BoundaryFloorRecovery.BaselinePolicy&&floor.GeometryHash.Length==64&&floor.AllowedAddedPenetration==PositionBound&&floor.Plane.Reviewed&&floor.Plane.Normal.SequenceEqual(new float[]{0,1,0}),
            "Floor receipt binds the explicit reviewed plane, complete geometry hash and authentic per-region added-penetration baseline");
        // Original invented proportions and body/foot volumes; no named character,
        // native index or source action is used by the implementation or oracle.
        foreach(var variation in new[]{
            Create(.72f,1.15f,.025f,.04f,false,false,17),
            Create(1.4f,.8f,.08f,.10f,false,false,29),
            Create(1.1f,1.3f,.035f,.05f,true,false,43),
            Create(.9f,.9f,.02f,.04f,true,true,61)})
        {
            var varied=Fit(variation);Verify(variation,varied,check,"proportions/volume "+variation.Project.Name);
        }
        var weighted=Create(1,1,.04f,.025f,true,true,83,1100);var weightedFit=Fit(weighted);
        Verify(weighted,weightedFit,check,"uncapped weighted geometry");
        check(weightedFit.Receipt.FloorRecovery!.Regions.All(r=>r.PointCount>1100&&r.LegacyPointCount<r.PointCount),
            "Complete foot regions retain more than 512 points and all tiny/partial positive influences instead of the legacy .8 subset or a sensor cap");
        // Insert a reviewed orientation control between retained actor travel
        // and the body. Its changing native parent matrix must drive the floor
        // direction; using the original parent's inverse is incorrect here.
        var controlBase=Create(1,1,.02f,.02f,true,true,101);
        int Map(int index)=>index<1?index:index+1;
        var controlBones=controlBase.Project.Scene.Bones.Select((b,i)=>b with{Parent=i==1?1:b.Parent<0?-1:Map(b.Parent)}).ToList();
        controlBones.Insert(1,new("reviewed_orientation",0,Transform.Identity));
        var controlScene=controlBase.Project.Scene with{Bones=controlBones.ToArray(),Meshes=controlBase.Project.Scene.Meshes.Select(m=>m with{Vertices=m.Vertices.Select(v=>v with{Joints=new Vector4(Map((int)v.Joints.X),Map((int)v.Joints.Y),Map((int)v.Joints.Z),Map((int)v.Joints.W))}).ToArray()}).ToArray()};
        var controlChannels=new Curve?[9];controlChannels[5]=new([new(0,15),new(12,15)]);controlChannels[7]=new([new(0,.08f),new(12,.08f)]);
        var controlSource=controlBase.Source with{Tracks=[..controlBase.Source.Tracks,new("reviewed_orientation",controlChannels)]};
        var controlSetup=controlBase.Project.ControlRigs[0] with{SkeletonFingerprint=AnimationControls.Fingerprint(controlScene),BodyBone=2,
            Chains=controlBase.Project.ControlRigs[0].Chains.Select(c=>c with{Root=Map(c.Root),Middle=Map(c.Middle),Effector=Map(c.Effector)}).ToArray(),
            BoneRoles=[new("RootMotion",0,"Reviewed actor placement",true),new("BodyOrientation",1,"Reviewed native ancestor axes",true),new("Hips",2,"Reviewed body",true)]};
        var controlFixture=controlBase with{Project=controlBase.Project with{Scene=controlScene,Clips=[controlSource,controlBase.Reference],ControlRigs=[controlSetup]},Source=controlSource,
            Settings=controlBase.Settings with{Bones=controlScene.Bones.Select(b=>b.Name).ToArray(),MatchReferenceControls=true},Body=2,Ankles=controlBase.Ankles.Select(Map).ToArray()};
        var controlFit=Fit(controlFixture);Verify(controlFixture,controlFit,check,"fitted reference parent under retained actor travel");
        check(controlFit.Receipt.FloorRecovery!.BaselinePolicy==BoundaryFloorRecovery.ReferenceControlBaselinePolicy&&controlFit.Receipt.MatchedReferenceControls.SequenceEqual(new[]{"reviewed_orientation"}),
            "Floor receipt distinguishes fitted reference controls from retained actor travel with no alternate geometry or limits");
        var changedVertex=weighted.Project.Scene.Meshes[0].Vertices.ToArray();changedVertex[^1]=changedVertex[^1] with{Position=changedVertex[^1].Position+new Vector3(.003f,0,0)};
        var changedScene=weighted.Project.Scene with{Meshes=[weighted.Project.Scene.Meshes[0] with{Vertices=changedVertex}]};
        var geometryChanged=Fit(weighted with{Project=weighted.Project with{Scene=changedScene}});
        check(geometryChanged.Receipt.FloorRecovery!.GeometryHash!=weightedFit.Receipt.FloorRecovery.GeometryHash,
            "The floor geometry receipt changes for authentic material outside a capped or dominant-weight subset");
        // Renaming exact identities and reversing clip/mask storage cannot alter
        // mathematical output; reviewed indices remain structural evidence only.
        var names=basic.Project.Scene.Bones.Select((_,i)=>"unfamiliar_"+(127-i)).ToArray();
        BoneTrack Rename(BoneTrack t)=>t with{Bone=names[Array.FindIndex(basic.Project.Scene.Bones,b=>b.Name==t.Bone)]};
        var renamedScene=basic.Project.Scene with{Bones=basic.Project.Scene.Bones.Select((b,i)=>b with{Name=names[i]}).ToArray()};
        var renamedSource=basic.Source with{Tracks=basic.Source.Tracks.Reverse().Select(Rename).ToArray()};var renamedReference=basic.Reference with{Tracks=basic.Reference.Tracks.Reverse().Select(Rename).ToArray()};
        var renamedProject=basic.Project with{Scene=renamedScene,Clips=[renamedReference,renamedSource],ControlRigs=basic.Project.ControlRigs.Select(r=>r with{SkeletonFingerprint=AnimationControls.Fingerprint(renamedScene)}).ToArray()};
        var renamed=basic with{Project=renamedProject,Source=renamedSource,Reference=renamedReference,Settings=basic.Settings with{Bones=names.Reverse().ToArray(),ContactBones=basic.Ankles.Select(i=>names[i]).Reverse().ToArray()}};
        var renamedFit=Fit(renamed);
        check(fit.Clip.Tracks.All(track=>track.Channels.Zip(renamedFit.Clip.Tracks.Single(t=>t.Bone==names[Array.FindIndex(basic.Project.Scene.Bones,b=>b.Name==track.Bone)]).Channels)
            .All(p=>p.First is null?p.Second is null:p.Second is not null&&p.First.Keys.SequenceEqual(p.Second.Keys))),
            "Shared floor recovery has identical keys/tangents under renamed bindings and reversed clip/mask storage");
        int[] order=[0,1,6,7,8,9,2,3,4,5,10];var map=order.Select((old,index)=>(old,index)).ToDictionary(p=>p.old,p=>p.index);
        var permutedScene=basic.Project.Scene with{
            Bones=order.Select(i=>basic.Project.Scene.Bones[i] with{Parent=basic.Project.Scene.Bones[i].Parent<0?-1:map[basic.Project.Scene.Bones[i].Parent]}).ToArray(),
            Meshes=basic.Project.Scene.Meshes.Select(m=>m with{Vertices=m.Vertices.Select(v=>v with{Joints=new(map[(int)v.Joints.X],map[(int)v.Joints.Y],map[(int)v.Joints.Z],map[(int)v.Joints.W])}).ToArray()}).ToArray()};
        var permutedSetup=basic.Project.ControlRigs.Select(r=>r with{SkeletonFingerprint=AnimationControls.Fingerprint(permutedScene),BodyBone=map[basic.Body],
            Chains=r.Chains.Select(c=>c with{Root=map[c.Root],Middle=map[c.Middle],Effector=map[c.Effector]}).Reverse().ToArray(),BoneRoles=r.BoneRoles.Select(b=>b with{Bone=map[b.Bone]}).ToArray()}).ToArray();
        var permuted=basic with{Project=basic.Project with{Scene=permutedScene,ControlRigs=permutedSetup},Ankles=basic.Ankles.Select(i=>map[i]).ToArray(),Body=map[basic.Body]};
        var permutedFit=Fit(permuted);
        check(fit.Clip.Tracks.All(track=>track.Channels.Zip(permutedFit.Clip.Tracks.Single(t=>t.Bone==track.Bone).Channels)
            .All(p=>p.First is null?p.Second is null:p.Second is not null&&p.First.Keys.SequenceEqual(p.Second.Keys))),
            "Reviewed hierarchy, full weight indices and reversed leg bindings can be structurally permuted without changing any fitted curves");
        var pairedChannels=new Curve?[9];float hipHeight=basic.Project.Scene.Bones[basic.Body].Rest.Translation.Y;
        pairedChannels[7]=new([new(0,hipHeight),new(4,hipHeight),new(4,hipHeight,.025f,.025f),new(12,hipHeight)]);
        var pairedSource=basic.Source with{Tracks=[..basic.Source.Tracks,new(basic.Project.Scene.Bones[basic.Body].Name,pairedChannels)]};
        var pairedFixture=basic with{Source=pairedSource};var pairedFit=Fit(pairedFixture);
        var pairedResult=pairedFit.Clip.Tracks.Single(t=>t.Bone==basic.Project.Scene.Bones[basic.Body].Name).Channels[7]!;
        check(pairedResult.Keys.Where(k=>k.Frame<=4).SequenceEqual(pairedChannels[7]!.Keys.Where(k=>k.Frame<=4))&&
            BoundaryFitting.Derivative(pairedResult,4,false)==0&&BoundaryFitting.Derivative(pairedResult,4)==.025f,
            "Clearance baking retains both original paired hip-translation join sides and exact incoming/outgoing tangents");
        Verify(pairedFixture,pairedFit,check,"paired hip join");
        var shiftedReference=basic.Reference with{Tracks=[new(basic.Project.Scene.Bones[0].Name,[new([new(0,1.8f)]),null,null,null,null,new([new(0,73)]),null,new([new(0,99)]),null]),..basic.Reference.Tracks.Where(t=>t.Bone!=basic.Project.Scene.Bones[0].Name)]};
        var placed=Fit(basic with{Reference=shiftedReference});
        check(placed.Clip.Tracks.SelectMany(t=>t.Channels).Zip(fit.Clip.Tracks.SelectMany(t=>t.Channels)).All(p=>p.First is null?p.Second is null:p.Second is not null&&p.First.Keys.SequenceEqual(p.Second.Keys)),
            "Reference root translation, scale and rotation cannot realign source actor placement or change body recovery");
        var moving=Create(1,1,.03f,.02f,true,true,97);var movingFit=Fit(moving);Verify(moving,movingFit,check,"vertical/rotating/nonuniform protected roots");
        check(moving.Source.Tracks[0].Channels[7]!.Keys[^1].Value!=moving.Reference.Sample(moving.Project.Scene.Bones[0],0).Translation.Y,
            "Moving-root fixture ends at a different authentic vertical source placement than the frozen reference root");
        bool Reject(Fixture f,string part)
        {try{Fit(f);return false;}catch(Exception error)when(error is InvalidDataException or NotSupportedException){return error.Message.Contains(part,StringComparison.OrdinalIgnoreCase);}}
        check(Reject(basic with{Settings=basic.Settings with{FloorSettings=basic.Settings.FloorSettings! with{Reviewed=false}}},"reviewed")&&
            Reject(basic with{Settings=basic.Settings with{FloorSettings=new([0,2,0],0,true,"Not normalized")}},"normalized"),
            "Missing review or non-unit support planes reject instead of inferring floor context");
        check(Reject(basic with{Settings=basic.Settings with{ExitContactRecovery=false}},"requires explicit"),"Floor correction cannot bypass explicit reviewed contact-path release");
        var noGeometry=basic with{Project=basic.Project with{Scene=basic.Project.Scene with{Meshes=[]}}};
        check(Reject(noGeometry,"geometry"),"Missing weighted foot geometry fails closed instead of substituting joint positions");
        var pin=new ContactPin(Guid.NewGuid(),"Original hard anchor",basic.Ankles[0],Vector3.Zero,Vector3.Zero,Quaternion.Identity,true,"Original reviewed fixture pin");
        check(Reject(basic with{Project=basic.Project with{AuthoringContacts=[pin]}},"pin")&&
            Reject(basic with{Project=basic.Project with{Contacts=[new(Guid.NewGuid(),basic.Source.Id,7,AnimationControls.Fingerprint(basic.Project.Scene),[pin]){EndFrame=10}]}},"interval"),
            "Floor correction never releases hard pins or timed contact intervals to create clearance");
        var impossible=Create(1,1,0,0,false,false,109,0,60);
        check(Reject(impossible,"20%"),"The independent sixty-degree two-link contradiction exceeds the fixed .4 lift limit and rejects without moving roots");
        check(Reject(basic with{Settings=basic.Settings with{ExitSamples=2,Protected=[new(0,11,"Prefix")] }},"envelope"),
            "Two endpoint-only native knots cannot hide a nonzero envelope; an infeasible final interval rejects");
        var singularChannels=moving.Source.Tracks[0].Channels.ToArray();singularChannels[1]=new([new(0,0),new(12,0)]);
        var singularSource=moving.Source with{Tracks=moving.Source.Tracks.Select((t,i)=>i==0?t with{Channels=singularChannels}:t).ToArray()};
        check(Reject(moving with{Source=singularSource},"singular"),"A noninvertible moving protected parent is reported as a contradiction rather than compensated on roots");
        var invalidVertices=basic.Project.Scene.Meshes[0].Vertices.ToArray();invalidVertices[0]=invalidVertices[0] with{Position=new(float.NaN,0,0)};
        check(Reject(basic with{Project=basic.Project with{Scene=basic.Project.Scene with{Meshes=[basic.Project.Scene.Meshes[0] with{Vertices=invalidVertices}]}}},"invalid authentic skin"),
            "Nonfinite weighted geometry rejects before a candidate or receipt can be published");
        string path=Path.Combine(dir,"floor-recovery.fighterport");await ProjectStore.SaveAsync(basic.Project with{Clips=[fit.Clip,basic.Reference]},path);var reopened=await ProjectStore.LoadAsync(path);
        var stored=reopened.Clips[0].Transition!.CurveFit!;
        check(stored.Version==BoundaryFitting.FloorVersion&&stored.ReferenceRootPolicy==BoundaryFitting.SourceRootReferencePolicy&&
            stored.FloorRecovery!.GeometryHash==floor.GeometryHash&&stored.FloorRecovery.BaselinePolicy==floor.BaselinePolicy&&stored.Settings.FloorSettings!.Evidence==floor.Plane.Evidence,
            "Project reopen retains explicit floor adaptation, immutable geometry proof and source-root reference placement policy");
        check(stored.FloorRecovery!.Regions.Select(r=>r.PointCount).SequenceEqual(floor.Regions.Select(r=>r.PointCount))&&reopened.Clips[0].Tracks.SelectMany(t=>t.Channels).Zip(fit.Clip.Tracks.SelectMany(t=>t.Channels))
            .All(p=>p.First is null?p.Second is null:p.Second is not null&&p.First.Keys.SequenceEqual(p.Second.Keys)),
            "Project reopen preserves complete region counts and all native integral Hermite key/tangent sides exactly");
    }
    static BoundaryFitResult Fit(Fixture f,BoundaryFitSettings? settings=null)=>BoundaryFitting.Prepare(f.Project,f.Source,null,new(f.Reference,AnimationControls.Fingerprint(f.Project.Scene),0),settings??f.Settings);
    static Fixture Create(float upper,float lower,float thickness,float width,bool moving,bool nonuniform,int identity,int many=0,float angle=45)
    {
        float h=upper+lower*MathF.Cos(angle*MathF.PI/180);var root=nonuniform?new Transform(new(1.1f,.85f,1.25f),new(0,12,7),Vector3.Zero):Transform.Identity;
        Bone B(int i,int parent,Vector3 translation)=>new("original_"+identity+"_"+(31-i),parent,new(Vector3.One,Vector3.Zero,translation));
        var bones=new[]{new Bone("actor_"+identity,-1,root),B(1,0,new(0,h,0)),B(2,1,new(-.3f,0,0)),B(3,2,new(0,-upper,0)),B(4,3,new(0,-lower,0)),B(5,4,new(0,0,.2f)),
            B(6,1,new(.3f,0,0)),B(7,6,new(0,-upper,0)),B(8,7,new(0,-lower,0)),B(9,8,new(0,0,.2f)),B(10,1,new(0,1,0))};
        var scene=new Scene("Original weighted fixture "+identity,bones,[]);var bind=Evaluate(scene,null,0);var vertices=new List<Vertex>();
        foreach(int ankle in new[]{4,8})
        {
            void Add(Vector3 offset,Vector4 joints,Vector4 weights)=>vertices.Add(new(Vector3.Transform(offset,bind.World[ankle]),Vector3.UnitY,Vector2.Zero,joints,weights,Vector4.One));
            Add(Vector3.Zero,new(ankle,0,0,0),new(1,0,0,0));
            if(thickness>0||width>0)
            {
                Add(new(-width,-thickness,.1f),new(ankle,0,0,0),new(1,0,0,0));Add(new(width,-thickness,-.1f),new(ankle,0,0,0),new(1,0,0,0));
                Add(new(0,-thickness*1.2f,0),new(ankle,1,0,0),new(.05f,.95f,0,0));
                Add(new(0,-thickness*1.1f,.05f),new(1,0,ankle,0),new(.999999f,0,.000001f,0));
            }
            for(int i=0;i<many;i++)Add(new(width*(i%3-1),-thickness*(i%5)/5,.04f*(i%7-3)),new(1,ankle,0,0),new(.25f,.75f,0,0));
        }
        scene=scene with{Meshes=[new("Original authentic full-weight foot geometry",vertices.ToArray(),[])]};
        var rootChannels=new Curve?[9];if(moving)
        {
            rootChannels[7]=new([new(0,.1f,-.02f,-.02f),new(12,-.14f,-.02f,-.02f)]);
            rootChannels[6]=new([new(0,0,.07f,.07f),new(12,.84f,.07f,.07f)]);
            rootChannels[5]=new([new(0,root.Rotation.Z,1,1),new(12,root.Rotation.Z+12,1,1)]);
            rootChannels[4]=new([new(0,root.Rotation.Y,.5f,.5f),new(12,root.Rotation.Y+6,.5f,.5f)]);
        }
        Curve?[] Knee(float a){var c=new Curve?[9];c[5]=new([new(0,a),new(12,a)]);return c;}
        var source=new AnimationClip(Guid.NewGuid(),"Original generic action "+identity,13,new(60),false,[new(bones[0].Name,rootChannels),new(bones[3].Name,Knee(angle)),new(bones[7].Name,Knee(angle))]);
        var reference=source with{Id=Guid.NewGuid(),Revision=Guid.NewGuid(),Name="Authentic first pose "+identity,Tracks=[new(bones[3].Name,Knee(-angle)),new(bones[7].Name,Knee(-angle))]};
        string fp=AnimationControls.Fingerprint(scene);
        var setup=new ControlRigSetup(Guid.NewGuid(),fp,[new("Foot.L",2,3,4,Vector3.UnitZ,"Reviewed original left structure",true),new("Foot.R",6,7,8,Vector3.UnitZ,"Reviewed original right structure",true)],[])
            {BodyBone=1,BoneRoles=[new("Hips",1,"Reviewed authentic body",true)]};
        var project=new FighterProject(1,Guid.NewGuid(),"rig_"+identity,scene,[source,reference]){ControlRigs=[setup]};
        var settings=new BoundaryFitSettings(0,9,TransformComponents.All,bones.Select(b=>b.Name).ToArray(),[new(0,4,"Complete original action prefix")])
            {PreserveContacts=true,ContactBones=[bones[4].Name,bones[8].Name],ExitContactRecovery=true,FloorSettings=new([0,1,0],0,true,"Explicit original static Y plane reviewed for this operation")};
        return new(project,source,reference,settings,[4,8],1);
    }
    static void Verify(Fixture f,BoundaryFitResult fit,Action<bool,string> check,string label)
    {
        var scene=f.Project.Scene;int join=f.Source.SampleCount-f.Settings.ExitSamples,last=f.Source.SampleCount-1;var body=scene.Bones[f.Body];
        bool root=ReferenceEquals(f.Source.Tracks[0],fit.Clip.Tracks.Single(t=>t.Bone==f.Source.Tracks[0].Bone));
        bool prefix=true,clearance=true;double max=0;
        var times=new SortedSet<double>();for(int i=0;i<=last*64;i++)times.Add(i/64d);for(int i=join;i<last;i++){times.Add(i+.37);times.Add(i+.73);}
        foreach(double at in times)
        {
            var original=Evaluate(scene,f.Source,at);var actual=Evaluate(scene,fit.Clip,at);
            root&=original.Local[0].Channels().SequenceEqual(actual.Local[0].Channels());
            if(at<=join)prefix&=original.Local.Zip(actual.Local).All(p=>Distance(p.First,p.Second));
            if(at<join)continue;
            var anchor=Evaluate(scene,f.Reference,0,new Dictionary<int,Transform>{{0,original.Local[0]}});
            foreach(int ankle in f.Ankles)
            {
                var points=scene.Meshes.SelectMany(m=>m.Vertices).Where(v=>Enumerable.Range(0,4).Any(slot=>v.Weights[slot]>0&&Descendant(scene,ankle,(int)v.Joints[slot]))).ToArray();
                double before=points.Min(v=>(double)Point(v,original.Skin).Y),reference=points.Min(v=>(double)Point(v,anchor.Skin).Y),after=points.Min(v=>(double)Point(v,actual.Skin).Y);
                double added=Math.Max(0,Math.Min(0,Math.Min(before,reference))-after);max=Math.Max(max,added);clearance&=added<=PositionBound;
            }
        }
        var end=Evaluate(scene,fit.Clip,last);var expected=Evaluate(scene,f.Reference,0,new Dictionary<int,Transform>{{0,f.Source.Sample(scene.Bones[0],last)}});
        bool endpoint=end.Local.Zip(expected.Local).All(p=>Distance(p.First,p.Second))&&end.World.Zip(expected.World).All(p=>MatrixNear(p.First,p.Second,PositionBound));
        check(clearance&&max<=PositionBound,label+": independent full-weight per-foot 1/64/.37/.73 clearance adds at most .0002 penetration beyond authentic baselines");
        check(prefix&&root,label+": prejoin local poses and every protected source-local root sample/key remain exact");
        check(endpoint,label+": endpoint reaches authentic first body pose under original source-root placement in local and world transforms");
        var hip=fit.Clip.Tracks.Single(t=>t.Bone==body.Name);
        check(hip.Channels.Skip(6).All(c=>c!.Keys.All(k=>k.Frame==Math.Truncate(k.Frame))&&BoundaryFitting.Derivative(c,last,false)==0&&BoundaryFitting.Derivative(c,last)==0),
            label+": only native integral translation knots are committed and frozen endpoint incoming/outgoing slopes remain zero");
        check(fit.Receipt.FloorRecovery is {Evaluations:<=200000,KnotCount:<=8192} receipt&&receipt.MaximumAddedPenetration<=PositionBound&&receipt.MaximumLift<=receipt.LiftLimit,
            label+": explicit bounded receipt reports actual region clearance and finite work/lift limits");
    }
    static bool Descendant(Scene scene,int parent,int bone){for(int i=bone;i>=0;i=scene.Bones[i].Parent)if(i==parent)return true;return false;}
    sealed record Pose(Transform[] Local,Matrix4x4[] World,Matrix4x4[] Skin);
    // Independent fixture oracle: direct authentic bind inverse and all four
    // original vertex weights, never the production region builder or envelope.
    static Pose Evaluate(Scene scene,AnimationClip? clip,double frame,IReadOnlyDictionary<int,Transform>? overrides=null)
    {
        var local=new Transform[scene.Bones.Length];var world=new Matrix4x4[local.Length];var bind=new Matrix4x4[local.Length];var skin=new Matrix4x4[local.Length];
        for(int i=0;i<local.Length;i++)
        {
            var bone=scene.Bones[i];local[i]=overrides?.GetValueOrDefault(i)??clip?.Sample(bone,frame)??bone.Rest;
            bind[i]=bone.Rest.Matrix*(bone.Parent<0?Matrix4x4.Identity:bind[bone.Parent]);world[i]=local[i].Matrix*(bone.Parent<0?Matrix4x4.Identity:world[bone.Parent]);
            Matrix4x4.Invert(bind[i],out var inverse);skin[i]=inverse*world[i];
        }
        return new(local,world,skin);
    }
    static Vector3 Point(Vertex v,Matrix4x4[] skin){var p=Vector3.Zero;for(int i=0;i<4;i++)if(v.Weights[i]>0)p+=Vector3.Transform(v.Position,skin[(int)v.Joints[i]])*v.Weights[i];return p;}
    static bool Distance(Transform a,Transform b)
    {
        if(Vector3.Distance(a.Translation,b.Translation)>PositionBound||a.Scale.ChannelsMax(b.Scale)>ScaleBound)return false;
        Quaternion Q(Vector3 r)=>Quaternion.CreateFromRotationMatrix(Matrix4x4.CreateRotationX(r.X*MathF.PI/180)*Matrix4x4.CreateRotationY(r.Y*MathF.PI/180)*Matrix4x4.CreateRotationZ(r.Z*MathF.PI/180));
        var qa=Q(a.Rotation);var qb=Q(b.Rotation);
        double na=Math.Sqrt((double)qa.X*qa.X+(double)qa.Y*qa.Y+(double)qa.Z*qa.Z+(double)qa.W*qa.W),nb=Math.Sqrt((double)qb.X*qb.X+(double)qb.Y*qb.Y+(double)qb.Z*qb.Z+(double)qb.W*qb.W);
        double dot=Math.Abs(((double)qa.X*qb.X+(double)qa.Y*qb.Y+(double)qa.Z*qb.Z+(double)qa.W*qb.W)/(na*nb));
        return 2*Math.Acos(Math.Min(1,dot))*180/Math.PI<=OrientationBound;
    }
    static float ChannelsMax(this Vector3 a,Vector3 b)=>Math.Max(Math.Abs(a.X-b.X),Math.Max(Math.Abs(a.Y-b.Y),Math.Abs(a.Z-b.Z)));
    static bool MatrixNear(Matrix4x4 a,Matrix4x4 b,double epsilon)=>new[]{a.M11-b.M11,a.M12-b.M12,a.M13-b.M13,a.M14-b.M14,a.M21-b.M21,a.M22-b.M22,a.M23-b.M23,a.M24-b.M24,a.M31-b.M31,a.M32-b.M32,a.M33-b.M33,a.M34-b.M34,a.M41-b.M41,a.M42-b.M42,a.M43-b.M43,a.M44-b.M44}.All(x=>Math.Abs(x)<=epsilon);
}
