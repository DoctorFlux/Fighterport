using System.Numerics;
using System.Text.Json;
using FighterPort.Core;
using FighterPort.Adapters.Brawl;

internal static class NativeRetargetV2Checks
{
    public static async Task RunAsync(BrawlAdapter adapter,string directory,Action<bool,string> check,bool moving=false)
    {
        Directory.CreateDirectory(directory);var fixtures=new List<FighterProject>();var originals=new List<(string Path,string Hash)>();
        for(int index=0;index<2;index++)
        {
            var authored=RetargetV2Checks.Chain(index==1);var bones=authored.Scene.Bones.ToArray();float tail=bones.Skip(3).Sum(b=>b.Rest.Translation.X);float height=tail*.5f;bones[1]=bones[1] with{Rest=bones[1].Rest with{Translation=new(0,height,0)}};
            var shape=authored.Scene with{Bones=bones,Meshes=[]};var rest=AnimationService.Evaluate(shape,null,0);var tip=rest.World[5].Translation;
            // The pinned skinless rigid DAE importer adds the bind placement to
            // positions before attaching the same bind again. Compensate only
            // this original fixture's interchange input, then verify the real
            // imported/native source skin against the independent ground label.
            var vertices=new[]{Vector3.Zero,new Vector3(0,.06f,-.04f),new Vector3(0,.06f,.04f),new Vector3(-.04f,.06f,0)}.Select(p=>new Vertex(p,Vector3.UnitY,Vector2.Zero,new(5,0,0,0),new(1,0,0,0),Vector4.One)).ToArray();shape=shape with{Meshes=[new("Original calibrated palm",vertices,[0,1,2,0,2,3,0,3,1,1,3,2])]};
            string dae=Path.Combine(directory,$"body-{index}.dae"),model=Path.Combine(directory,$"body-{index}.mdl0"),stub=Path.Combine(directory,$"template-{index}.chr0");FixtureDae.Write(shape,dae,attributes:true);
            await adapter.RunAsync(new("fixture",dae,stub,null,model,[],[]));var imported=await adapter.ImportModelAsync(model);originals.Add((model,ProjectStore.Hash(model)));
            int Native(int bone)=>Array.FindIndex(imported.Scene.Bones,b=>b.Name==shape.Bones[bone].Name);
            check(Enumerable.Range(0,6).All(i=>Native(i)>=0),"Native V2 fixture import retains every authored native chain identity");
            var profile=authored.ControlRigs[0] with{SkeletonFingerprint=AnimationControls.Fingerprint(imported.Scene),BodyBone=Native(1),BoneRoles=authored.ControlRigs[0].BoneRoles.Select(r=>r with{Bone=Native(r.Bone)}).ToArray(),VirtualChains=[new("Ground support limb",Enumerable.Range(1,5).Select(Native).ToArray(),"Authored five-node palm support chain",true)],JointLimits=Enumerable.Range(1,5).Select(i=>new NativeJointLimit(Native(i),new(-170),new(170),"Authored mechanical joint bounds",true)).ToArray()};
            var clip=new AnimationClip(Guid.NewGuid(),"Original ground-palm support",8,new(60),false,[new(imported.Scene.Bones[Native(2)].Name,[null,null,null,null,null,new(moving?[new(0,-15),new(3,-30),new(7,-30)]:[new(0,-30),new(7,-30)]),null,null,null])]);
            // Independently constructed geometry: one first segment stays level;
            // the remaining lengths rotate -30 degrees. Palm vertex zero is at
            // the endpoint; the other vertices are above this fixed Y=0 plane.
            float x=bones[2].Rest.Translation.X+tail*MathF.Cos(MathF.PI/6);var goal=new Vector3(x,0,0);
            var pose=AnimationService.Evaluate(imported.Scene,clip,moving?3:0);check(Vector3.Distance(pose.World[Native(5)].Translation,goal)<.00001f,"Independent trigonometric source palm label agrees with the actual imported native geometry");
            var sourcePoints=imported.Scene.Meshes.SelectMany(m=>m.Vertices).Select(v=>new SkinSurfacePoint(v.Position,v.Joints,v.Weights)).ToArray();
            check(sourcePoints.Min(p=>ContactGeometry.Point(p,pose).Y)>=-.00001f&&sourcePoints.Any(p=>Vector3.Distance(ContactGeometry.Point(p,pose),goal)<.00001f),"Independent source support label covers the actual native palm skin, not just its bone origin");
            var contact=new ContactFrame(Guid.NewGuid(),clip.Id,moving?3:0,profile.SkeletonFingerprint,[new ContactPin(Guid.NewGuid(),"Ground palm",Native(5),Vector3.Zero,goal,Quaternion.CreateFromAxisAngle(Vector3.UnitZ,-MathF.PI/6),true,"Authored fixed ground-palm frame; Y=0"){Clearance=new(Vector3.UnitY,0,sourcePoints)}]){EndFrame=8,EndExclusive=true};
            var authoredSource=imported with{Clips=[clip],ControlRigs=[profile],Contacts=[contact]};string sourceMotion=Path.Combine(directory,$"source-{index}.chr0");await adapter.ExportAsync(authoredSource,clip,sourceMotion);
            var authentic=await adapter.ImportAsync(model,sourceMotion);var nativeClip=authentic.Clips.Single();
            check(nativeClip.SampleCount==8&&nativeClip.Tracks.Single(t=>t.Bone==clip.Tracks[0].Bone).Channels[5]!.Sample(moving?3:0)==-30,"Native V2 source motion is genuinely encoded and freshly decoded before retargeting");
            nativeClip=nativeClip with{Source=nativeClip.Source! with{AssociationEvidence="Independently authored fixture model/clip association verified by expected native keys and geometry"}};originals.Add((sourceMotion,ProjectStore.Hash(sourceMotion)));
            fixtures.Add(authentic with{Clips=[nativeClip],ControlRigs=[profile],Contacts=[contact with{Clip=nativeClip.Id}],MotionCoverage=moving?[new(nativeClip.Revision,0,3d/60,false,"Independently authored airborne approach",true),new(nativeClip.Revision,3d/60,nativeClip.DurationSeconds,true,"Independent fixture ground-palm position/orientation and interval",true)]:[new(nativeClip.Revision,0,nativeClip.DurationSeconds,true,"Independent fixture ground-palm position/orientation and interval",true)]});
            await ProjectStore.SaveAsync(fixtures[^1],Path.Combine(directory,$"source-{index}.fighterport"));
        }
        var receipts=new List<object>();
        foreach(var (source,target,direction) in new[]{(fixtures[0],fixtures[1],"short-to-long-distribution"),(fixtures[1],fixtures[0],"long-to-short-distribution")})
        {
            var sourceRig=source.ControlRigs[0];var targetRig=target.ControlRigs[0];string deform=AnimationControls.DeformChecksum(target.Scene);
            var result=RetargetService.Bake(source,source.Clips[0],sourceRig,target with{Clips=[]},targetRig);
            check(result.Value is not null,$"Native V2 {direction} produces a solved five-node ground-support motion: "+string.Join("; ",result.Diagnostics.Where(d=>d.Error).Select(d=>d.Message)));
            var bake=result.Value!;var validation=RetargetAcceptance.Measure(target,targetRig,bake);
            await File.WriteAllTextAsync(Path.Combine(directory,direction+"-prepared.json"),JsonSerializer.Serialize(new{Validation=validation,Diagnostics=result.Diagnostics,Pipeline=bake.Clip.Retarget!.Pipeline,Tracks=bake.Clip.Tracks},new JsonSerializerOptions(ProjectStore.Json){WriteIndented=true,IgnoreReadOnlyProperties=false}));
            check(validation.Passed&&validation.RequiredContactSamples>0&&validation.OrientationRadians<=.001f,$"Native V2 {direction} meets frozen position and orientation bounds before export ({validation.Separation:G9}/{validation.PositionBound:G9} at {validation.WorstContactFrame:G12}; rotation {validation.OrientationRadians:G9})");
            string output=Path.Combine(directory,direction+".chr0");var project=RetargetService.Apply(target with{Clips=[]},bake);await adapter.ExportAsync(project,bake.Clip,output);
            double[] times=Enumerable.Range(0,32).Select(i=>i*.25).Concat([.125,.2,1d/3,2.875,3.125,3.2,3d+1d/3,3.375,6.8,7.875,validation.WorstContactFrame,validation.WorstFloorFrame]).Distinct().Order().ToArray();var fresh=await adapter.RunAsync(new("import",target.SourceModels[0].Resource.Path,output,null,null,[],times));var reopened=fresh.Project!;
            float boneError=0,skinError=0,palmError=0,depth=0;int effector=targetRig.BoneRoles.Single(r=>r.SemanticRole=="Part.5").Bone;
            // Independent authored correspondence for these two fixtures; do not
            // obtain the expected target contact from the generated bake.
            var anchor=new Vector3(direction=="short-to-long-distribution"?(1+3*MathF.Cos(MathF.PI/6))*.925f:(1.8f+1.9f*MathF.Cos(MathF.PI/6))*(4/3.7f),0,0);
            check(Vector3.Distance(bake.Contacts[0].Pins[0].Position,anchor)<.000001f,$"Native V2 {direction} target anchor matches independent authored geometry and proportion policy");
            var mesh=target.Scene.Meshes.SelectMany(m=>m.Vertices).ToArray();
            for(int sample=0;sample<times.Length;sample++)
            {
                var pose=AnimationService.Evaluate(target.Scene,bake.Clip,times[sample]);
                for(int bone=0;bone<target.Scene.Bones.Length;bone++){var p=fresh.ExpectedBonePositions![sample][bone];boneError=Math.Max(boneError,Vector3.Distance(pose.World[bone].Translation,new(p[0],p[1],p[2])));}
                var e=fresh.ExpectedBonePositions![sample][effector];if(!moving||times[sample]>=3)palmError=Math.Max(palmError,Vector3.Distance(anchor,new(e[0],e[1],e[2])));
                for(int v=0;v<mesh.Length;v++){var p=fresh.ExpectedVertices![sample][v];skinError=Math.Max(skinError,Vector3.Distance(AnimationService.Skin(mesh[v],pose),new(p[0],p[1],p[2])));depth=Math.Max(depth,Math.Max(0,-p[1]));}
            }
            check(boneError<.0003f&&skinError<.0003f,$"Native V2 {direction} freshly reopened bones/skin match the independent worker ({boneError:G9}/{skinError:G9})");
            check(palmError<=validation.PositionBound&&depth<=validation.PenetrationBound,$"Native V2 {direction} independently reopened palm/ground geometry remains constrained ({palmError:G9}/{depth:G9})");
            var decoded=RetargetAcceptance.Measure(reopened,targetRig,bake with{Clip=reopened.Clips[0] with{Retarget=bake.Clip.Retarget}});check(decoded.Passed&&reopened.Clips[0].SampleCount==8&&!reopened.Clips[0].Loop&&reopened.Clips[0].LastKeyFrame==7,$"Native V2 {direction} fresh decoded constraints and exact native duration/last-key/loop policy pass");
            bool exact=true;int compared=0;
            foreach(var track in bake.Clip.Tracks)for(int channel=0;channel<9;channel++)if(track.Channels[channel] is {} expected)
            {
                var actual=reopened.Clips[0].Tracks.Single(t=>t.Bone==track.Bone).Channels[channel];
                // A native constant may legitimately omit redundant keys. Every
                // represented varying value and tangent must retain exact bits.
                if(expected.Keys.All(k=>k.Value==expected.Keys[0].Value&&k.InTangent==0&&k.OutTangent==0))
                {exact&=actual is not null&&BitConverter.SingleToInt32Bits(actual.Sample(0))==BitConverter.SingleToInt32Bits(expected.Keys[0].Value);compared++;continue;}
                exact&=actual is not null&&actual.Keys.Length==expected.Keys.Length;
                if(actual is null||actual.Keys.Length!=expected.Keys.Length)continue;
                for(int k=0;k<expected.Keys.Length;k++){var a=actual.Keys[k];var e=expected.Keys[k];exact&=a.Frame==e.Frame&&BitConverter.SingleToInt32Bits(a.Value)==BitConverter.SingleToInt32Bits(e.Value)&&BitConverter.SingleToInt32Bits(a.InTangent)==BitConverter.SingleToInt32Bits(e.InTangent)&&BitConverter.SingleToInt32Bits(a.OutTangent)==BitConverter.SingleToInt32Bits(e.OutTangent);compared++;}
            }
            check(exact&&compared>0,$"Native V2 {direction} fresh decode retains exact supported key values and tangent bits ({compared} comparisons)");
            check(deform==AnimationControls.DeformChecksum(reopened.Scene)&&originals.All(p=>ProjectStore.Hash(p.Path)==p.Hash),$"Native V2 {direction} preserves both original native models, skeletons and skin weights");
            receipts.Add(new{Direction=direction,Backend=bake.Clip.Retarget!.Backend,Validation=validation,Decoded=decoded,BoneError=boneError,SkinError=skinError,PalmError=palmError,GroundDepth=depth,ExactNativeKeys=exact,NativeComparisons=compared,NativeSampleFrames=times,Pipeline=bake.Clip.Retarget.Pipeline});
        }
        await File.WriteAllTextAsync(Path.Combine(directory,"native-v2-receipt.json"),JsonSerializer.Serialize(receipts,new JsonSerializerOptions(ProjectStore.Json){WriteIndented=true,IgnoreReadOnlyProperties=false}));
    }
}
