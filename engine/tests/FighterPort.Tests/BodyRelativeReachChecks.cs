using System.Numerics;
using FighterPort.Core;

internal static class BodyRelativeReachChecks
{
    sealed record Fixture(Scene Scene,ControlRigSetup Rig,int[] Path);
    static readonly RigAxes Standard=new(Vector3.UnitX,Vector3.UnitY,Vector3.UnitZ);
    public static void Run(Action<bool,string> check)
    {
        var source=Create("opaque-a",[Vector3.UnitX,Vector3.UnitX,Vector3.UnitX]);
        var target=Create("opaque-b",[new(2,0,0),new(2,0,0),new(2,0,0),new(2,0,0)]);
        string sourceBefore=RetargetDependencies.Hash(new{source.Scene,source.Rig}),targetBefore=RetargetDependencies.Hash(new{target.Scene,target.Rig});
        var goals=Prepare(source,target);var binding=goals.Bindings.Single();
        check(binding.SourceBones.Length==4&&binding.TargetBones.Length==5&&Math.Abs(binding.NativeLengthRatio-8f/3)<.000001f&&goals.Diagnostics.Length==0,"Body-relative reach retains reviewed four/five-joint native paths without requiring three adjacent bones");
        var rest=goals.Evaluate(Pose(source),Pose(target)).Single();
        check(Vector3.Distance(rest.Position,new(8,0,0))<.00001f&&rest.JointHints.Select(h=>h.Position).Zip(new[]{new Vector3(2,0,0),new(4,0,0),new(6,0,0)}).All(p=>Vector3.Distance(p.First,p.Second)<.00001f),"Source rest reproduces independently authored target rest endpoint and every native interior point");
        var turned=goals.Evaluate(Pose(source,root:90),Pose(target,root:90)).Single();
        check(Vector3.Distance(turned.Position,new(0,8,0))<.00002f&&Math.Abs(turned.RequestedDistance-8)<.00002f&&Math.Abs(turned.SourceExtension-1)<.000001f,"A ninety-degree source action turns the target's own eight-unit reach rather than retaining a source-length endpoint");
        check(!rest.HasBendEvidence&&rest.BendDirection==Vector3.Zero&&rest.JointHints.Length==3,"A straight path retains individual shape hints without inventing a world-axis bend pole");

        var offsetSource=Create("offset-a",[Vector3.UnitX,Vector3.UnitX,Vector3.UnitX],new(1,2,0));
        var offsetTarget=Create("offset-b",[new(2,0,0),new(2,0,0),new(2,0,0),new(2,0,0)],new(3,4,0));
        var offsetGoals=Prepare(offsetSource,offsetTarget);
        var displaced=offsetGoals.Evaluate(Pose(offsetSource,root:90,anchorTranslation:new(-8,30,0)),Pose(offsetTarget,root:90,anchorTranslation:new(10,20,0))).Single();
        check(displaced.RootPosition==new Vector3(13,24,0)&&Vector3.Distance(displaced.Position,new(13,32,0))<.00003f,"Body-relative goals use the actual posed native chain root and follow independently moved target parents");
        var rotated=offsetGoals.Evaluate(Pose(offsetSource,root:90,anchor:90,anchorTranslation:new(-8,30,0)),Pose(offsetTarget,root:90,anchor:90,anchorTranslation:new(10,20,0))).Single();
        check(Vector3.Distance(rotated.RootPosition,new(6,23,0))<.00002f&&Vector3.Distance(rotated.Position,new(-2,23,0))<.00004f,"Goals strip the source parent turn and compose the target parent turn exactly once");

        var bentSource=Create("curve-a",[Vector3.UnitX,Vector3.UnitX]);
        var bentTarget=Create("curve-b",[new(1.5f,0,0),new(.5f,0,0)]);
        var unequal=Prepare(bentSource,bentTarget).Evaluate(Pose(bentSource,middle:90),Pose(bentTarget,middle:90)).Single();
        check(Vector3.Distance(unequal.Position,new(1,1,0))<.00001f&&Vector3.Distance(Pose(bentTarget,middle:90).World[bentTarget.Path[^1]].Translation,new(1.5f,.5f,0))<.00001f,"Unequal segment proportions produce the independent one-by-one action goal rather than the mismatched baseline endpoint");
        check(Vector3.Distance(unequal.JointHints.Single().Position,new(1,.5f,0))<.00001f&&Vector3.Dot(unequal.BendDirection,Vector3.Normalize(new Vector3(1,-1,0)))>.99999f&&unequal.HasBendEvidence,"Rest-arc shape hints retain the source bend side as soft geometry without pretending the hint has the target's fixed segment length");
        check(Math.Abs(unequal.SourceExtension-MathF.Sqrt(2)/2)<.00001f&&Math.Abs(unequal.RequestedDistance-MathF.Sqrt(2))<.00001f,"Reach reporting measures independent source extension and requested target distance without clamping");
        var reverse=Prepare(bentTarget,bentSource).Evaluate(Pose(bentTarget,middle:90),Pose(bentSource,middle:90)).Single();
        check(Vector3.Distance(reverse.Position,new(1.5f,.5f,0))<.00001f,"The reverse unequal-segment direction derives its own goal rather than inverting a forward solve");

        var shapeTarget=Create("rest-shape",[new(3,0,0),new(0,1,0)]);
        var shape=Prepare(bentSource,shapeTarget).Evaluate(Pose(bentSource),Pose(shapeTarget)).Single();
        check(shape.Position==new Vector3(3,1,0)&&shape.JointHints.Single().Position==new Vector3(3,0,0),"A different target rest bend/shape is preserved by a rest residual rather than forced into a scaled source rest line");
        check(!shape.SourceBendObserved&&shape.HasBendEvidence&&shape.BendProvenance.StartsWith("Reviewed target rest-shape prior",StringComparison.Ordinal)&&Pose(bentSource).World[bentSource.Path[1]].Translation==Vector3.UnitX,
            "An independently straight source reports the bent target-rest prior separately instead of claiming an observed source bend");
        check(unequal.SourceBendObserved&&unequal.BendProvenance.StartsWith("Source-pose geometry",StringComparison.Ordinal)&&Pose(bentSource,middle:90).World[bentSource.Path[^1]].Translation.Y>.99999f,
            "A genuinely bent source retains its actual geometric bend provenance independently of target-rest priors");
        var rigidShape=Prepare(bentSource,shapeTarget).Evaluate(Pose(bentSource,root:90),Pose(shapeTarget,root:90)).Single();
        check(Vector3.Distance(rigidShape.Position,new(-1,3,0))<.00003f&&rigidShape.RequestedDistance<rigidShape.Binding.TargetRestLength,"A rigid root turn rotates the target's own bent rest shape; a feasible four-unit chain is never assigned the false sqrt(26)-unit reach");
        var reverseShape=Prepare(shapeTarget,bentSource).Evaluate(Pose(shapeTarget,root:90),Pose(bentSource,root:90)).Single();
        check(Vector3.Distance(reverseShape.Position,new(0,2,0))<.00003f,"Reverse bent-rest transfer preserves the straight target's rigid two-unit turn instead of manufacturing a one-unit folded goal");
        var axisShapeSource=Create("shape-axis-a",[Vector3.UnitX,Vector3.UnitX],anchorRest:90);
        var axisShapeTarget=Create("shape-axis-b",[new(3,0,0),new(0,1,0)],anchorRest:-90);
        var axisShape=Prepare(axisShapeSource,axisShapeTarget);
        check(Vector3.Distance(axisShape.Evaluate(Pose(axisShapeSource,root:90),Pose(axisShapeTarget,root:90)).Single().Position,new(3,1,0))<.00004f,"Rotated source/target parent rests preserve an independent rigid turn of different target rest geometry");
        check(Vector3.Distance(axisShape.Evaluate(Pose(axisShapeSource,root:90,anchor:180),Pose(axisShapeTarget,root:90,anchor:0)).Single().Position,new(-1,3,0))<.00004f,"Changed parent rests, a moving body frame and a local root turn compose the target rest-shape residual once");

        var scaledShape=Prepare(bentSource,shapeTarget).Evaluate(Pose(bentSource,rootScale:2),Pose(shapeTarget,rootScale:2)).Single();
        check(Vector3.Distance(scaledShape.Position,new(6,2,0))<.00003f,"Authored uniform chain-root scale transforms the target rest-shape residual; it never manufactures the incorrect seven-by-one reach");
        var reverseScaledShape=Prepare(shapeTarget,bentSource).Evaluate(Pose(shapeTarget,root:90,rootScale:2),Pose(bentSource,root:90,rootScale:2)).Single();
        check(Vector3.Distance(reverseScaledShape.Position,new(0,4,0))<.00004f,"Reverse bent-rest transfer preserves authored root scale and rotation without shrinking or folding the target span");
        var ancestorScaledShape=Prepare(bentSource,shapeTarget).Evaluate(Pose(bentSource,root:90,anchorScale:2),Pose(shapeTarget,root:90,anchorScale:2)).Single();
        check(Vector3.Distance(ancestorScaledShape.Position,new(-2,6,0))<.00005f,"Authored scale above the chain follows the rotated target rest residual without being normalized from the parent frame");
        var reverseAncestorScaled=Prepare(shapeTarget,bentSource).Evaluate(Pose(shapeTarget,rootScale:2,anchorScale:3),Pose(bentSource,rootScale:2,anchorScale:3)).Single();
        check(Vector3.Distance(reverseAncestorScaled.Position,new(12,0,0))<.00006f,"Reverse transfer composes root and ancestor authored scales once while keeping native rest lengths unchanged");
        var scaledRestSource=RestScale(bentSource,.5f);var scaledRestTarget=RestScale(shapeTarget,2);
        var scaledRestGoal=Prepare(scaledRestSource,scaledRestTarget).Evaluate(Pose(scaledRestSource,rootScale:1),Pose(scaledRestTarget,rootScale:4)).Single();
        check(Vector3.Distance(scaledRestGoal.Position,new(12,4,0))<.00006f&&Math.Abs(scaledRestGoal.Binding.NativeLengthRatio-8)<.00001f,"Different native rest scales are removed only from the motion delta, preserving target rest geometry and authored relative scale");
        var scaledAxes=axisShape.Evaluate(Pose(axisShapeSource,root:90,anchor:180,rootScale:2,anchorScale:3),Pose(axisShapeTarget,root:90,anchor:0,rootScale:2,anchorScale:3)).Single();
        check(Vector3.Distance(scaledAxes.Position,new(-6,18,0))<.0001f,"Different parent rests, root turns and both native scale levels compose in independently specified Cartesian coordinates");
        bool sourceSingular=false,targetSingular=false,restSingular=false;
        try{Prepare(bentSource,shapeTarget).Evaluate(Pose(bentSource,rootScale:0),Pose(shapeTarget));}catch(NotSupportedException e){sourceSingular=e.Message.Contains("singular",StringComparison.OrdinalIgnoreCase);}
        try{Prepare(bentSource,shapeTarget).Evaluate(Pose(bentSource),Pose(shapeTarget,anchorScale:0));}catch(NotSupportedException e){targetSingular=e.Message.Contains("singular",StringComparison.OrdinalIgnoreCase);}
        try{Prepare(bentSource,RestScale(shapeTarget,0));}catch(NotSupportedException e){restSingular=e.Message.Contains("singular",StringComparison.OrdinalIgnoreCase);}
        check(sourceSingular&&targetSingular&&restSingular,"Singular animated source/target ancestry and native rest scale are rejected explicitly instead of normalized or treated as zero reach");

        var identityEdits=shapeTarget.Scene.Bones.Select((b,i)=>(Index:i,Value:b.Rest)).ToDictionary(p=>p.Index,p=>p.Value);
        identityEdits[1]=identityEdits[1] with{Scale=new(2),Rotation=new(10,-15,25),Translation=new(3,-2,1)};
        identityEdits[shapeTarget.Path[0]]=identityEdits[shapeTarget.Path[0]] with{Scale=new(1.2f,.8f,1.3f),Rotation=new(-20,35,70),Translation=new(.2f,.3f,-.1f)};
        identityEdits[shapeTarget.Path[1]]=identityEdits[shapeTarget.Path[1]] with{Rotation=new(12,34,-57),Translation=new(2.8f,.2f,.3f)};
        identityEdits[shapeTarget.Path[^1]]=identityEdits[shapeTarget.Path[^1]] with{Scale=new(.9f,1.1f,.8f),Rotation=new(400,-35,27),Translation=new(.1f,1.2f,-.3f)};
        var identityPose=AnimationService.Evaluate(shapeTarget.Scene,null,0,identityEdits);
        string identityBefore=RetargetDependencies.Hash(new{shapeTarget.Scene,shapeTarget.Rig,Pose=identityPose});
        var identityGoals=Prepare(shapeTarget,shapeTarget).Evaluate(identityPose,identityPose).Single();
        var identitySolved=NativeChainReach.Solve(RigDescriptor.Prepare(shapeTarget.Scene,shapeTarget.Rig),identityPose.Local,shapeTarget.Path,identityGoals.Position,identityGoals.HasBendEvidence?identityGoals.BendDirection:null);
        check(Vector3.Distance(identityGoals.Position,identityPose.World[shapeTarget.Path[^1]].Translation)<.00002f&&identityGoals.JointHints.All(h=>Vector3.Distance(h.Position,identityPose.World[h.TargetBone].Translation)<.00002f),"Same-rig source goals retain every actual native point under authored nonuniform scale, translation and full-turn rotation");
        check(identitySolved.Status=="AlreadyMatched"&&identitySolved.Local.SequenceEqual(identityPose.Local)&&identityBefore==RetargetDependencies.Hash(new{shapeTarget.Scene,shapeTarget.Rig,Pose=identityPose}),"Already-matched same-rig fitting preserves all native local channels exactly, including winding, authored scale and translation; source/deform data remain immutable");
        var helperSource=Create("helper-a",[Vector3.UnitX,Vector3.Zero,Vector3.UnitX]);
        var helperTarget=Create("helper-b",[Vector3.UnitX,Vector3.Zero,Vector3.UnitX,new(2,0,0)]);
        var helper=Prepare(helperSource,helperTarget).Evaluate(Pose(helperSource,root:90),Pose(helperTarget,root:90)).Single();
        check(helper.Binding.SourceBones.Length==4&&helper.Binding.TargetBones.Length==5&&Vector3.Distance(helper.Position,new(0,4,0))<.00002f&&helper.JointHints.All(h=>PoseGeometry.Finite(h.Position)),"Zero-length native helpers remain in complete paths while rest-arc interpolation stays finite");

        var differentSource=Create("axis-rest-a",[Vector3.UnitX,Vector3.UnitX,Vector3.UnitX],anchorRest:90);
        var differentTarget=Create("axis-rest-b",[new(-2,0,0),new(-2,0,0),new(-2,0,0)],anchorRest:-90);
        var different=Prepare(differentSource,differentTarget);
        var parentTurn=different.Evaluate(Pose(differentSource,anchor:180),Pose(differentTarget,anchor:0)).Single();
        check(Vector3.Distance(parentTurn.Position,new(-6,0,0))<.00004f,"Different native parent rest axes preserve an independently specified quarter-turn of the target body frame");
        var parentAndJoint=different.Evaluate(Pose(differentSource,root:90,anchor:180),Pose(differentTarget,root:90,anchor:0)).Single();
        check(Vector3.Distance(parentAndJoint.Position,new(0,-6,0))<.00004f,"Native parent rest conversion and an additional joint turn compose without a doubled axis correction");

        var units=ChangeCoordinates(source,100,90,false);var unitGoals=Prepare(source,units);
        var centimetres=unitGoals.Evaluate(Pose(source,root:90),Pose(units,root:90)).Single();
        check(Math.Abs(centimetres.Binding.NativeLengthRatio-100)<.0001f&&Vector3.Distance(centimetres.Position,new(-300,0,0))<.001f,"Native chain ratios contain centimetre conversion once and honor the independently rotated model coordinate frame");
        var metres=Prepare(units,source).Evaluate(Pose(units,root:90),Pose(source,root:90)).Single();
        check(Math.Abs(metres.Binding.NativeLengthRatio-.01f)<.000001f&&Vector3.Distance(metres.Position,new(0,3,0))<.00003f,"Reverse native-unit and coordinate conversion independently returns the three-unit action goal");
        var reflected=ChangeCoordinates(source,1,0,true);
        var reflectedGoal=Prepare(source,reflected).Evaluate(Pose(source,root:45),Pose(reflected,root:-45)).Single();
        check(Vector3.Distance(reflectedGoal.Position,new(-3/MathF.Sqrt(2),3/MathF.Sqrt(2),0))<.00003f,"Reviewed reflected coordinate frames transform reach vectors without replacing native binds or inventing a reflection quaternion");

        var shuffledSource=Permute(source);var shuffledTarget=Permute(target);
        var shuffled=Prepare(shuffledSource,shuffledTarget).Evaluate(Pose(shuffledSource,root:90),Pose(shuffledTarget,root:90)).Single();
        check(shuffled.Binding.SourceBones[0]!=binding.SourceBones[0]&&shuffled.Binding.TargetBones[0]!=binding.TargetBones[0]&&Vector3.Distance(shuffled.Position,turned.Position)<.00001f,"Equivalent renamed rigs and valid sibling-storage permutations produce the same mapped reach goal");
        var closest=goals.Bindings.Single();
        check(closest.SourceAnchor==1&&closest.TargetAnchor==1,"The nearest consistent reviewed ancestor is selected instead of the actor root farther up the native graph");
        var withoutAnchor=Mapping(source,target) with{Bones=Mapping(source,target).Bones.Where(p=>p.Source>=2&&p.Target>=2).ToArray()};
        var missing=new BodyRelativeReachGoals(RigDescriptor.Prepare(source.Scene,source.Rig),RigDescriptor.Prepare(target.Scene,target.Rig),withoutAnchor);
        check(missing.Bindings.Length==0&&missing.Diagnostics.Any(d=>d.Code=="RIG_BINDING_REVIEW"),"Missing parent correspondence produces a precise review result instead of a guessed frame or global bone index");
        var unreviewed=source with{Rig=source.Rig with{VirtualChains=[source.Rig.VirtualChains![0] with{Reviewed=false}]}};
        check(Prepare(unreviewed,target).Bindings.Length==0,"An unreviewed virtual chain cannot quietly become an authoritative reach binding");
        var candidateMap=Mapping(source,target) with{Bones=Mapping(source,target).Bones.Select(p=>p.Source==source.Path[0]?p with{Binding=p.Binding! with{Supplied=false}}:p).ToArray()};
        check(new BodyRelativeReachGoals(RigDescriptor.Prepare(source.Scene,source.Rig),RigDescriptor.Prepare(target.Scene,target.Rig),candidateMap).Bindings.Length==0,"An inferred root candidate is not treated as supplied reviewed correspondence");
        var crossed=Mapping(source,target) with{Bones=Mapping(source,target).Bones.Select(p=>p.Source==0?p with{Target=1}:p.Source==1?p with{Target=0}:p).ToArray()};
        var incompatible=new BodyRelativeReachGoals(RigDescriptor.Prepare(source.Scene,source.Rig),RigDescriptor.Prepare(target.Scene,target.Rig),crossed);
        check(incompatible.Bindings.Length==0&&incompatible.Diagnostics.Length>0,"Crossed mapped ancestor order is reported rather than silently selecting one incompatible body frame");
        var descriptor=RigDescriptor.Prepare(source.Scene,source.Rig);
        var duplicate=descriptor with{Binding=descriptor.Binding with{Chains=descriptor.Binding.Chains.Append(descriptor.Binding.Chains.Single() with{Evidence="Independent second role declaration"}).ToArray()}};
        var ambiguousRole=new BodyRelativeReachGoals(duplicate,RigDescriptor.Prepare(target.Scene,target.Rig),Mapping(source,target));
        check(ambiguousRole.Bindings.Length==0&&ambiguousRole.Diagnostics.Any(d=>d.Code=="RIG_BINDING_REVIEW"&&d.Message.Contains("Source chain role is ambiguous",StringComparison.Ordinal))&&descriptor.Binding.Chains.Length==1,
            "Duplicate source chain roles produce explicit review without a guessed binding, dictionary collision or mutation of the original descriptor");

        var sampleSource=Pose(bentSource,middle:90);var sampleTarget=Pose(bentTarget,middle:90);var deterministic=Prepare(bentSource,bentTarget);
        string samplesBefore=RetargetDependencies.Hash(new{Source=sampleSource,Target=sampleTarget});var first=deterministic.Evaluate(sampleSource,sampleTarget).Single();
        foreach(float angle in new[]{-90f,0,45,180,-30})deterministic.Evaluate(Pose(bentSource,middle:angle),Pose(bentTarget,middle:angle));
        var again=deterministic.Evaluate(sampleSource,sampleTarget).Single();
        check(first.Position==again.Position&&first.BendDirection==again.BendDirection&&first.JointHints.SequenceEqual(again.JointHints),"Random evaluation order cannot change prepared reach goals, bend evidence or native joint hints");
        check(samplesBefore==RetargetDependencies.Hash(new{Source=sampleSource,Target=sampleTarget})&&sourceBefore==RetargetDependencies.Hash(new{source.Scene,source.Rig})&&targetBefore==RetargetDependencies.Hash(new{target.Scene,target.Rig}),"Reach preparation/evaluation leaves original native bones, rest transforms, skin weights, rig data and supplied poses unchanged");
    }

    static BodyRelativeReachGoals Prepare(Fixture source,Fixture target)=>new(RigDescriptor.Prepare(source.Scene,source.Rig),RigDescriptor.Prepare(target.Scene,target.Rig),Mapping(source,target));
    static RetargetMappingReport Mapping(Fixture source,Fixture target)
    {
        var a=source.Rig.BoneRoles;var b=target.Rig.BoneRoles;
        return new(a.Where(r=>b.Any(t=>t.SemanticRole==r.SemanticRole)).Select(r=>new RetargetBonePair(r.SemanticRole,r.Bone,b.Single(t=>t.SemanticRole==r.SemanticRole).Bone,"Independently supplied Cartesian fixture bindings")
        {Binding=new("Supplied fixture correspondence","Reviewed",true,true,"Independent source binding","Independent target binding")}).ToArray(),[],[]);
    }
    static Fixture Create(string prefix,Vector3[] segments,Vector3 rootOffset=default,float anchorRest=0)
    {
        var bones=new List<Bone>{new(prefix+"_actor",-1,Transform.Identity),new(prefix+"_anchor",0,Transform.Identity with{Rotation=new(0,0,anchorRest)}),new(prefix+"_root",1,Transform.Identity with{Translation=rootOffset})};
        foreach(var segment in segments)bones.Add(new(prefix+"_native_"+bones.Count,bones.Count-1,Transform.Identity with{Translation=segment}));
        int end=bones.Count-1,spare=bones.Count;var path=Enumerable.Range(2,segments.Length+1).ToArray();
        bones.Add(new(prefix+"_spare",1,Transform.Identity with{Translation=new(0,0,2)}));bones.Add(new(prefix+"_spare_leaf",spare,Transform.Identity with{Translation=Vector3.UnitY}));
        var scene=new Scene(prefix,bones.ToArray(),[]);var rest=AnimationService.Evaluate(scene,null,0);var position=rest.World[end].Translation;
        var vertices=new[]{position+new Vector3(.1f,0,0),position+new Vector3(0,.1f,0),position+new Vector3(0,0,.1f)}.Select(v=>new Vertex(v,Vector3.UnitZ,Vector2.Zero,new(end,2,0,0),new(.6f,.4f,0,0),Vector4.One)).ToArray();
        scene=scene with{Meshes=[new("Original weighted triangle",vertices,[0,1,2])]};
        var rig=new ControlRigSetup(Guid.NewGuid(),AnimationControls.Fingerprint(scene),[],[]){Axes=Standard,Coordinates=new(1,"Independent Cartesian fixture units"),
            VirtualChains=[new("Reviewed appendage",path,"Complete independently specified native path",true)],
            BoneRoles=[new("RootMotion",0,"Reviewed actor",true),new("Reviewed.Anchor",1,"Reviewed parent",true),new("Reviewed.Root",2,"Reviewed native root",true),new("Reviewed.End",end,"Reviewed native endpoint",true),new("Reviewed.Spare",spare,"Reviewed independent sibling",true),new("Reviewed.SpareLeaf",spare+1,"Reviewed sibling child",true)]};
        return new(scene,rig,path);
    }
    static EvaluatedPose Pose(Fixture fixture,float root=0,float? anchor=null,float middle=0,Vector3? anchorTranslation=null,float? rootScale=null,float? anchorScale=null)
    {
        var edits=new Dictionary<int,Transform>{{fixture.Path[0],fixture.Scene.Bones[fixture.Path[0]].Rest with{Rotation=new(0,0,root),Scale=rootScale.HasValue?new(rootScale.Value):fixture.Scene.Bones[fixture.Path[0]].Rest.Scale}}};
        if(middle!=0)edits[fixture.Path[1]]=fixture.Scene.Bones[fixture.Path[1]].Rest with{Rotation=new(0,0,middle)};
        int body=fixture.Rig.BoneRoles.Single(r=>r.SemanticRole=="Reviewed.Anchor").Bone;
        if(anchor.HasValue||anchorTranslation.HasValue||anchorScale.HasValue)edits[body]=fixture.Scene.Bones[body].Rest with{Rotation=anchor.HasValue?new(0,0,anchor.Value):fixture.Scene.Bones[body].Rest.Rotation,Translation=anchorTranslation??fixture.Scene.Bones[body].Rest.Translation,Scale=anchorScale.HasValue?new(anchorScale.Value):fixture.Scene.Bones[body].Rest.Scale};
        return AnimationService.Evaluate(fixture.Scene,null,0,edits);
    }
    static Fixture RestScale(Fixture source,float scale)
    {
        var scene=source.Scene with{SkeletonRevision=Guid.NewGuid(),Bones=source.Scene.Bones.Select((b,i)=>i==source.Path[0]?b with{Rest=b.Rest with{Scale=new(scale)}}:b).ToArray()};
        return new(scene,source.Rig with{Id=Guid.NewGuid(),SkeletonFingerprint=AnimationControls.Fingerprint(scene)},source.Path.ToArray());
    }
    static Fixture ChangeCoordinates(Fixture source,float units,float rotation,bool reflection)
    {
        var matrix=reflection?Matrix4x4.CreateScale(-1,1,1):Matrix4x4.CreateRotationZ(rotation*MathF.PI/180);
        var bones=source.Scene.Bones.Select((b,i)=>b with{Name="coordinate_native_"+i,Rest=b.Rest with{Translation=(reflection?new Vector3(-b.Rest.Translation.X,b.Rest.Translation.Y,b.Rest.Translation.Z):b.Rest.Translation)*units,Rotation=i==0?new(0,0,rotation):b.Rest.Rotation}}).ToArray();
        var scene=source.Scene with{Name="Independent converted frame",SkeletonRevision=Guid.NewGuid(),Bones=bones,Meshes=source.Scene.Meshes.Select(m=>m with{Vertices=m.Vertices.Select(v=>v with{Position=Vector3.Transform(v.Position,matrix)*units,Normal=Vector3.TransformNormal(v.Normal,matrix)}).ToArray()}).ToArray()};
        var rig=source.Rig with{Id=Guid.NewGuid(),SkeletonFingerprint=AnimationControls.Fingerprint(scene),Coordinates=new(units,"Independently supplied native units"),Axes=reflection?new(-Vector3.UnitX,Vector3.UnitY,Vector3.UnitZ):new(Vector3.UnitY,-Vector3.UnitX,Vector3.UnitZ)};
        return new(scene,rig,source.Path.ToArray());
    }
    static Fixture Permute(Fixture source)
    {
        int spare=source.Scene.Bones.Length-2;var order=new[]{0,1,spare,spare+1}.Concat(source.Path).ToArray();var index=order.Select((old,i)=>(old,i)).ToDictionary(p=>p.old,p=>p.i);
        var scene=source.Scene with{SkeletonRevision=Guid.NewGuid(),Bones=order.Select((old,i)=>source.Scene.Bones[old] with{Name="unrelated_native_"+i,Parent=source.Scene.Bones[old].Parent<0?-1:index[source.Scene.Bones[old].Parent]}).ToArray(),
            Meshes=source.Scene.Meshes.Select(m=>m with{Vertices=m.Vertices.Select(v=>v with{Joints=new(index[(int)v.Joints.X],index[(int)v.Joints.Y],index[(int)v.Joints.Z],index[(int)v.Joints.W])}).ToArray()}).ToArray()};
        var rig=source.Rig with{Id=Guid.NewGuid(),SkeletonFingerprint=AnimationControls.Fingerprint(scene),BoneRoles=source.Rig.BoneRoles.Select(r=>r with{Bone=index[r.Bone]}).ToArray(),VirtualChains=source.Rig.VirtualChains!.Select(c=>c with{Bones=c.Bones.Select(b=>index[b]).ToArray()}).ToArray()};
        return new(scene,rig,source.Path.Select(b=>index[b]).ToArray());
    }
}
