using FighterPort.Adapters.Melee;
using HSDRaw;
using HSDRaw.Common;
using HSDRaw.Common.Animation;
using HSDRaw.GX;
using HSDRaw.Melee.Pl;
using HSDRaw.Tools;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
static class NativeCases
{
    static int checks;
    static void Check(bool value,string message){if(!value)throw new Exception(message);checks++;Console.WriteLine("PASS "+message);}
    static void Reject(Action action,string message){bool rejected=false;try{action();}catch(Exception e)when(e is InvalidDataException||e is IOException||e is ArgumentException||e is NotSupportedException||e is OperationCanceledException){rejected=true;}Check(rejected,message);}
    internal static HSD_JOBJ Joint(float x=0)=>new HSD_JOBJ{SX=1,SY=1,SZ=1,TX=x,Flags=JOBJ_FLAG.CLASSICAL_SCALING};
    static byte[] Bytes(HSDRawFile file){using(var s=new MemoryStream()){file.Save(s,optimize:false,trim:false);return s.ToArray();}}
    internal static MeleeNativeModel Model(int influences,bool mesh)
    {
        var root=Joint();root.Flags|=JOBJ_FLAG.SKELETON_ROOT|JOBJ_FLAG.ENVELOPE_MODEL;
        var joints=new List<HSD_JOBJ>();HSD_JOBJ previous=null;
        for(int i=0;i<influences;i++){var child=Joint(i*.25f);joints.Add(child);if(previous==null)root.Child=child;else previous.Next=child;previous=child;}
        if(mesh)
        {
            var vertices=new List<GX_Vertex>{new GX_Vertex{POS=new GXVector3(-1,0,0),NRM=new GXVector3(0,0,1)},new GX_Vertex{POS=new GXVector3(1,0,0),NRM=new GXVector3(0,0,1)},new GX_Vertex{POS=new GXVector3(0,2,0),NRM=new GXVector3(0,0,1)}};
            var generator=new POBJ_Generator{RoundWeights=false,UseTriangleStrips=false};
            var weights=Enumerable.Range(0,3).Select(_=>Enumerable.Repeat(1f/influences,influences).ToArray()).ToList();
            var bones=Enumerable.Range(0,3).Select(_=>joints.ToArray()).ToList();
            var pobj=generator.CreatePOBJsFromTriangleList(vertices,new[]{GXAttribName.GX_VA_PNMTXIDX,GXAttribName.GX_VA_POS,GXAttribName.GX_VA_NRM},bones,weights);generator.SaveChanges();
            root.Dobj=new HSD_DOBJ{Pobj=pobj,Mobj=new HSD_MOBJ{RenderFlags=RENDER_MODE.CONSTANT,Material=new HSD_Material{DiffuseColor=System.Drawing.Color.White,AmbientColor=System.Drawing.Color.White,Alpha=1}}};
        }
        var file=new HSDRawFile();file.Roots.Add(new HSDRootNode{Name="PlyProcedural5K_Share_joint",Data=root});
        return MeleeNative.ReadMembers(Bytes(file)).SelectMany(MeleeNative.Models).Single();
    }
    static MeleeNativeMotion Motion()
    {
        var translation=new HSD_FOBJ();translation.SetKeys(new List<FOBJKey>{new FOBJKey{Frame=0,Value=0,Tan=1,InterpolationType=GXInterpolationType.HSD_A_OP_SPL},new FOBJKey{Frame=4,Value=0,Tan=-1,InterpolationType=GXInterpolationType.HSD_A_OP_SPL}},JointTrackType.HSD_A_J_TRAX,0);
        var turn=new HSD_FOBJ();turn.SetKeys(new List<FOBJKey>{new FOBJKey{Frame=0,Value=0,InterpolationType=GXInterpolationType.HSD_A_OP_LIN},new FOBJKey{Frame=4,Value=720,InterpolationType=GXInterpolationType.HSD_A_OP_LIN}},JointTrackType.HSD_A_J_ROTY,0);
        var tx=new HSD_Track();tx.FromFOBJ(translation);var ry=new HSD_Track();ry.FromFOBJ(turn);
        var figa=new HSD_FigaTree{Type=1,FrameCount=4,Nodes=new List<FigaTreeNode>{new FigaTreeNode{Tracks=new List<HSD_Track>{tx,ry}}}};
        var file=new HSDRawFile();file.Roots.Add(new HSDRootNode{Name="PlyProcedural5K_Share_ACTION_Test_figatree",Data=figa});return MeleeNative.ReadMembers(Bytes(file)).SelectMany(MeleeNative.Motions).Single();
    }
    public static void Run(string output)
    {
        Directory.CreateDirectory(output);
        var nativeKeys=new[]{new MeleeCurveKey{Frame=0,Value=0,Incoming=1,Outgoing=1},new MeleeCurveKey{Frame=2,Value=2,Incoming=0,Outgoing=-1},new MeleeCurveKey{Frame=4,Value=0,Incoming=0}};
        var preciseTrack=MeleeMotionWriter.Encode(6,nativeKeys);var precisePlayer=new FOBJ_Player{TrackType=preciseTrack.TrackType,Keys=preciseTrack.GetKeys()};
        Check(Math.Abs(precisePlayer.GetValue(1)-1.25f)<.000001,"New native float writer preserves incoming Hermite slope: frozen midpoint 1.25");
        Check(Math.Abs(precisePlayer.GetValue(3)-.75f)<.000001,"New native float writer preserves distinct outgoing slope: frozen midpoint 0.75");
        Check(precisePlayer.GetValue(4)==0,"New native float writer preserves the actual terminal value");
        var rawOffset=MeleeMotionWriter.Encode(6,new[]{new MeleeCurveKey{Frame=0,Value=0,Incoming=1,Outgoing=1},new MeleeCurveKey{Frame=4,Value=4,Incoming=1,Outgoing=1}});
        MeleeNativeMotion OffsetMotion(short offset)
        {
            rawOffset.StartFrame=offset;var f=new HSDRawFile();f.Roots.Add(new HSDRootNode{Name="PlyOffset_Share_ACTION_Test_figatree",Data=new HSD_FigaTree{Type=1,FrameCount=4,Nodes=new List<FigaTreeNode>{new FigaTreeNode{Tracks=new List<HSD_Track>{rawOffset}}}}});return MeleeNative.ReadMembers(Bytes(f)).SelectMany(MeleeNative.Motions).Single();
        }
        var offsetRig=Model(2,false);offsetRig.Root.TX=13;var offsetPlayer=new MeleePlayback(offsetRig,OffsetMotion(2));
        Check(offsetPlayer.Sample(0,0)[6]==2&&offsetPlayer.Sample(0,.5f)[6]==2.5f&&offsetPlayer.Sample(0,2)[6]==4,"Positive native start offset evaluates raw Hermite time plus offset without rebaking a prefix");
        offsetPlayer=new MeleePlayback(offsetRig,OffsetMotion(-2));
        Check(offsetPlayer.Sample(0,0)[6]==13&&offsetPlayer.Sample(0,1)[6]==13&&offsetPlayer.Sample(0,2)[6]==0&&offsetPlayer.Sample(0,3)[6]==1,"Negative native start offset retains the authentic bind until the runtime starts the track");
        Reject(()=>MeleeMotionWriter.Encode(6,new[]{new MeleeCurveKey{Frame=0},new MeleeCurveKey{Frame=1.5f}}),"Fractional FOBJ waits are rejected, never truncated");
        var editedMotion=Motion();var motionCandidate=MeleeMotionWriter.Prepare(editedMotion,new[]{new MeleeCurveEdit{Joint=0,Axis=6,Keys=nativeKeys}},4);
        string cancelledOutput=Path.Combine(output,"cancelled.dat");
        using(var cancelled=new CancellationTokenSource())
        {Reject(()=>MeleeNativeWriter.Publish(motionCandidate,cancelledOutput,new string[0],cancelled.Token,()=>cancelled.Cancel()),"Cancellation immediately before native publication commits no output");Check(!File.Exists(cancelledOutput),"Cancelled native publication leaves no destination");}
        string appeared=Path.Combine(output,"appeared.dat");byte[] sentinel={9,8,7};
        Reject(()=>MeleeNativeWriter.Publish(motionCandidate,appeared,new string[0],default,()=>File.WriteAllBytes(appeared,sentinel)),"A destination appearing before native rename rejects publication");
        Check(File.ReadAllBytes(appeared).SequenceEqual(sentinel),"Failed native publication preserves the existing destination bytes");
        string stale=Path.Combine(output,"stale.dat");Reject(()=>MeleeNativeWriter.Publish(motionCandidate,stale,new string[0],default,()=>throw new InvalidDataException("stale fixture")),"Changed native export dependencies reject the final commit");
        Check(!File.Exists(stale)&&!Directory.GetFiles(output,"*.candidate").Any(),"Rejected native candidates leave no destination or temporary file");
        var decodedMotion=MeleeNative.ReadMembers(motionCandidate.CopyBytes()).SelectMany(MeleeNative.Motions).Single();
        var decodedPlayer=new MeleePlayback(Model(5,false),decodedMotion);
        Check(Math.Abs(decodedPlayer.Sample(0,1)[6]-1.25f)<.000001&&Math.Abs(decodedPlayer.Sample(0,3)[6]-.75f)<.000001,"Edited native action is evaluated after complete fresh DAT serialization");
        Check(decodedPlayer.Sample(0,4)[4]==720,"Editing translation leaves the authored full-turn rotation unchanged");
        var extended=MeleeMotionWriter.Prepare(editedMotion,new[]{new MeleeCurveEdit{Joint=3,Axis=6,Keys=new[]{new MeleeCurveKey{Frame=0,Value=9,Incoming=1,Outgoing=1},new MeleeCurveKey{Frame=4,Value=13,Incoming=1,Outgoing=1}}}},4,default,6);
        var extendedMotion=MeleeNative.ReadMembers(extended.CopyBytes()).SelectMany(MeleeNative.Motions).Single();
        Check(new MeleePlayback(Model(5,false),extendedMotion).Sample(3,2)[6]==11,"Explicit native model membership allows animation of an existing previously unanimated helper row");
        Reject(()=>MeleeMotionWriter.Prepare(editedMotion,new[]{new MeleeCurveEdit{Joint=6,Axis=6,Keys=nativeKeys}},4,default,6),"Native writing never invents a bone outside the explicit target model");
        var model=Model(5,false);var motion=Motion();var sampler=new MeleePlayback(model,motion);
        Check(sampler.JointCount==6,"Native hierarchy retains unweighted helper joints");
        var scaleRig=Model(2,false);var parent=scaleRig.Root;var child=parent.Child;parent.SX=2;parent.SY=3;parent.SZ=4;parent.Flags&=~JOBJ_FLAG.CLASSICAL_SCALING;
        child.TX=1;child.TY=2;child.TZ=3;child.RZ=(float)(Math.PI/2);
        var nativePose=new MeleePose(scaleRig);var point=System.Numerics.Vector3.Transform(System.Numerics.Vector3.UnitX,nativePose.World[1]);
        Check(System.Numerics.Vector3.Distance(point,new System.Numerics.Vector3(2,8,12))<.00001,"Frozen unequal-parent-scale witness follows native HSD compensation rather than ordinary inherited SRT");
        parent.Flags|=JOBJ_FLAG.CLASSICAL_SCALING;nativePose=new MeleePose(scaleRig);point=System.Numerics.Vector3.Transform(System.Numerics.Vector3.UnitX,nativePose.World[1]);
        Check(System.Numerics.Vector3.Distance(point,new System.Numerics.Vector3(2,9,12))<.00001,"Classical-scale flag retains its distinct native parent-scale behavior");
        var envelopeRig=Model(2,false);var joint1=envelopeRig.Root.Child;var joint2=joint1.Next;joint1.TX=10;joint2.TX=10;
        joint1.InverseWorldTransform=new HSD_Matrix4x3{M11=1,M22=1,M33=1,M14=-5};joint2.InverseWorldTransform=new HSD_Matrix4x3{M11=1,M22=1,M33=1,M14=-5};
        var envelopePose=new MeleePose(envelopeRig);var nativeMesh=new MeleeMesh{OwnerJoint=0,NativeEnvelope=true};var rawVertex=new MeleeVertex{NativePosition=new System.Numerics.Vector3(1,0,0)};rawVertex.Weights.Add(Tuple.Create(1,.5f));rawVertex.Weights.Add(Tuple.Create(2,.5f));
        point=System.Numerics.Vector3.Transform(rawVertex.NativePosition,envelopePose.VertexMatrix(nativeMesh,rawVertex));
        Check(Math.Abs(point.X-6)<.000001,"Authored envelope matrix is authoritative even when it differs from a reconstructed rest inverse");
        rawVertex.Weights.Clear();rawVertex.Weights.Add(Tuple.Create(1,1f));point=System.Numerics.Vector3.Transform(rawVertex.NativePosition,envelopePose.VertexMatrix(nativeMesh,rawVertex));
        Check(Math.Abs(point.X-11)<.000001,"Native root-owned rigid envelope uses its joint matrix without an invented inverse bind");
        joint1.Flags|=JOBJ_FLAG.SKELETON;joint1.InverseWorldTransform=new HSD_Matrix4x3{M11=1,M22=1,M33=1,M14=-2};envelopePose=new MeleePose(envelopeRig);nativeMesh.OwnerJoint=1;rawVertex.Weights.Clear();rawVertex.Weights.Add(Tuple.Create(2,1f));point=System.Numerics.Vector3.Transform(rawVertex.NativePosition,envelopePose.VertexMatrix(nativeMesh,rawVertex));
        Check(Math.Abs(point.X-8)<.000001,"Non-root envelope owner uses its native skeleton-node basis");
        var animatedScaleRig=Model(2,false);animatedScaleRig.Root.SX=2;animatedScaleRig.Root.SY=3;animatedScaleRig.Root.SZ=4;
        animatedScaleRig.Root.Flags&=~JOBJ_FLAG.CLASSICAL_SCALING;animatedScaleRig.Root.Child.RZ=(float)(Math.PI/2);
        var animatedScalePlayer=new MeleePlayback(animatedScaleRig,motion);var animatedScalePose=new MeleePose(animatedScaleRig);
        animatedScalePose.Sample(animatedScalePlayer,0);point=System.Numerics.Vector3.Transform(System.Numerics.Vector3.UnitX,animatedScalePose.World[1]);
        Check(Math.Abs(point.Y-3)<.00001&&(animatedScaleRig.Root.Flags&JOBJ_FLAG.CLASSICAL_SCALING)==0,"Figatree type overrides animated-joint scale behavior without mutating original native flags");
        animatedScalePose.Sample(null,-1);point=System.Numerics.Vector3.Transform(System.Numerics.Vector3.UnitX,animatedScalePose.World[1]);
        Check(Math.Abs(point.Y-2)<.00001,"Bind display restores original native scale semantics after an animation override");
        var authoredValues=Enumerable.Range(0,animatedScalePlayer.JointCount).Select(i=>animatedScalePlayer.Sample(i,0)).ToArray();
        var authoredClassical=Enumerable.Range(0,animatedScalePlayer.JointCount).Select(i=>animatedScalePlayer.UsesClassicalScale(i,(MeleeNative.Joints(animatedScaleRig.Root)[i].Flags&JOBJ_FLAG.CLASSICAL_SCALING)!=0)).ToArray();
        var authoredPose=new MeleePose(animatedScaleRig);authoredPose.SampleAuthored(authoredValues,authoredClassical);animatedScalePose.Sample(animatedScalePlayer,0);
        Check(authoredPose.World.SequenceEqual(animatedScalePose.World),"Editable native pose math matches native playback for every joint with unequal parent scales");
        var sharedMesh=new MeleeMesh{OwnerJoint=1,SharedJoint=2};rawVertex.MatrixIndex=0;
        point=System.Numerics.Vector3.Transform(rawVertex.NativePosition,envelopePose.VertexMatrix(sharedMesh,rawVertex));
        Check(Math.Abs(point.X-11)<.000001,"Shared display-list matrix zero uses owner joint directly");
        rawVertex.MatrixIndex=1;point=System.Numerics.Vector3.Transform(rawVertex.NativePosition,envelopePose.VertexMatrix(sharedMesh,rawVertex));
        Check(Math.Abs(point.X-11)<.000001,"Shared display-list matrix one uses the explicitly referenced native joint");
        rawVertex.MatrixIndex=2;Reject(()=>envelopePose.VertexMatrix(sharedMesh,rawVertex),"Invalid shared native matrix index is rejected rather than guessed");
        var highGroup=new SBM_LookupTable{LookupEntries=new HSDArrayAccessor<SBM_LookupEntry>{Array=new[]{new SBM_LookupEntry{Entries=new byte[]{0,1}},new SBM_LookupEntry{Entries=new byte[]{2}}}}};
        var lowGroup=new SBM_LookupTable{LookupEntries=new HSDArrayAccessor<SBM_LookupEntry>{Array=new[]{new SBM_LookupEntry{Entries=new byte[]{1,3}}}}};
        var visibilityTables=new SBM_PlayerModelLookupTables{CostumeVisibilityLookups=new HSDArrayAccessor<SBM_CostumeLookupTable>{Array=new[]{new SBM_CostumeLookupTable{HighPoly=new HSDArrayAccessor<SBM_LookupTable>{Array=new[]{highGroup}},LowPoly=new HSDArrayAccessor<SBM_LookupTable>{Array=new[]{lowGroup}}}}}};
        visibilityTables._s.SetInt32(0,1);
        var visibility=MeleePartVisibility.Create(visibilityTables,4,"fixture", "procedural");
        Check(visibility.IsNormalDetail(1)&&!visibility.IsNormalDetail(3),"Normal-detail membership permits shared high/low objects and excludes low-only geometry");
        Check(visibility.Snapshot().SequenceEqual(new[]{true,true,false,false}),"Selected high-detail parts restore shared DOBJ visibility after low-detail initialization");
        visibility.Select(0,1);Check(visibility.Snapshot().SequenceEqual(new[]{false,false,true,false}),"Native visibility variants hide alternate parts without deleting their source geometry");
        visibility.Select(0,-1);Check(!visibility.Snapshot().Any(v=>v),"Explicit hidden part group retains no alternate variant");
        Reject(()=>visibility.Select(0,2),"Out-of-range visibility variant is rejected");
        var alternate=new SBM_CostumeLookupTable{HighPoly=new HSDArrayAccessor<SBM_LookupTable>{Array=new[]{lowGroup}}};
        visibilityTables.CostumeVisibilityLookups.Array=new[]{visibilityTables.CostumeVisibilityLookups[0],alternate};
        Reject(()=>MeleePartVisibility.Create(visibilityTables,4,"fixture","procedural"),"Different costume tables require an explicit binding instead of guessing costume zero");
        var emptyVisibility=new SBM_PlayerModelLookupTables{CostumeVisibilityLookups=new HSDArrayAccessor<SBM_CostumeLookupTable>{Array=new[]{new SBM_CostumeLookupTable{HighPoly=new HSDArrayAccessor<SBM_LookupTable>{Array=new[]{new SBM_LookupTable()}}}}}};emptyVisibility._s.SetInt32(0,1);
        var emptyParts=MeleePartVisibility.Create(emptyVisibility,4,"fixture","empty-group");
        Check(emptyParts.Choice(0)==-1,"Empty native visibility groups retain a valid hidden choice for Cancel restoration");emptyParts.Select(0,-1);
        var dol=new byte[1024];Action<int,uint> u32=(o,v)=>{dol[o]=(byte)(v>>24);dol[o+1]=(byte)(v>>16);dol[o+2]=(byte)(v>>8);dol[o+3]=(byte)v;};
        u32(0,256);u32(0x48,0x80001000);u32(0x90,768);int stringOffset=512;
        Func<string,uint> text=value=>{int start=stringOffset;var bytes=System.Text.Encoding.ASCII.GetBytes(value);Array.Copy(bytes,0,dol,start,bytes.Length);stringOffset+=bytes.Length+1;return (uint)(0x80001000+start-256);};
        u32(256,text("PlFixture.dat"));u32(260,text("PlyFixture5K_Share_joint"));u32(268,text("PlAlternate.dat"));u32(272,text("PlyFixture5KAlt_Share_joint"));
        Check(MeleeCostumeBinding.FindIndex(dol,"PlAlternate.dat","PlyFixture5KAlt_Share_joint","PlyFixture5K",2)==1,"Independent native pointer records bind an alternative costume without a filename or character index table");
        Check(MeleeCostumeBinding.FindIndex(dol,"PlFixture.dat","PlyFixture5K_Share_joint","PlyFixture5K",2)==0,"Canonical native filename and root symbol agree on costume zero");
        Reject(()=>MeleeCostumeBinding.FindIndex(dol,"Other.dat","PlyFixture5K_Share_joint","PlyFixture5K",2),"Unknown model filename cannot borrow another costume binding");
        using(var cancelledBinding=new CancellationTokenSource()){cancelledBinding.Cancel();Reject(()=>MeleeCostumeBinding.FindIndex(dol,"PlFixture.dat","PlyFixture5K_Share_joint","PlyFixture5K",2,cancelledBinding.Token),"Cancelled executable binding never publishes a guessed costume");}
        u32(0x90,uint.MaxValue);Reject(()=>MeleeCostumeBinding.FindIndex(dol,"PlFixture.dat","PlyFixture5K_Share_joint","PlyFixture5K",2),"Out-of-bounds executable sections are rejected before native pointer reads");
        Check(Math.Abs(sampler.Sample(0,2)[6]-1)<.000001,"Independent Hermite midpoint proves equal key values with nonzero slopes are not a hold");
        Check(sampler.Sample(0,2)[4]==360&&sampler.Sample(0,4)[4]==720,"Native scalar sampling preserves authored full turns without wrapping");
        Check(sampler.Sample(1,2)[0]==1&&sampler.Sample(1,2)[6]==0,"Absent native tracks retain authentic bind values");
        string hash=MeleeSource.Sha256(model.Member.CopyOriginalBytes());
        for(int i=0;i<33;i++)sampler.Sample(0,i/8f);
        Check(hash==MeleeSource.Sha256(model.Member.CopyOriginalBytes()),"Prepared playback preserves exact source member bytes");
        var candidate=MeleeNativeWriter.PrepareOriginalSnapshot(model.Member);string exported=Path.Combine(output,"verified-model.dat");
        MeleeNativeWriter.Publish(candidate,exported,new string[0]);
        Check(MeleeNativeWriter.GraphHash(new HSDRawFile(File.ReadAllBytes(exported)))==candidate.GraphSha256,"Native model export reopens with the exact complete pointer/data graph");
        var second=MeleeNativeWriter.PrepareOriginalSnapshot(MeleeNative.ReadMembers(File.ReadAllBytes(exported)).Single());
        Check(second.GraphSha256==candidate.GraphSha256,"Native export/reopen/re-export preserves its complete graph witness");
        string destinationHash=MeleeSource.Sha256(File.ReadAllBytes(exported));Reject(()=>MeleeNativeWriter.Publish(candidate,exported,new string[0]),"Native export refuses an existing destination without publication");
        Check(MeleeSource.Sha256(File.ReadAllBytes(exported))==destinationHash,"Failed native publication preserves the existing file");
        byte[] sourceBefore=model.Member.CopyOriginalBytes();
        var edited=MeleeNativeWriter.PrepareEditedSnapshot(model.Member,file=>((HSD_JOBJ)file.Roots[0].Data).Child.TX=3.25f);
        var editedModel=MeleeNative.ReadMembers(edited.CopyBytes()).SelectMany(MeleeNative.Models).Single();
        Check(editedModel.Root.Child.TX==3.25f,"Edited native joint data freshly reopens with the exact authored value");
        Check(edited.CopyBytes().Zip(sourceBefore,(a,b)=>a==b).Count(same=>!same)<=4,"Same-layout native joint edit changes only its scalar bytes");
        Check(model.Member.CopyOriginalBytes().SequenceEqual(sourceBefore),"Native edited writing never mutates the original resource");
        var invalid=model.Member.CopyOriginalBytes();invalid[3]^=1;Reject(()=>MeleeNative.ValidateDat(invalid),"DAT physical length mismatch is rejected");
        invalid=model.Member.CopyOriginalBytes();invalid[4]=0x7f;Reject(()=>MeleeNative.ValidateDat(invalid),"DAT out-of-range relocation table is rejected");
        Reject(()=>MeleeSource.ValidateVirtualPath("../Pl.dat"),"Parent-traversal source member is rejected");
        Reject(()=>MeleeSource.ValidateVirtualPath("/Pl.dat"),"Rooted source member is rejected");
        Reject(()=>MeleeSource.ValidateVirtualPath("a\\Pl.dat"),"Mixed filesystem separator is rejected");
        Reject(()=>MeleeSource.ValidateVirtualPath("a:Pl.dat"),"Alternate stream/member syntax is rejected");
        var cycle=Joint();cycle.Child=cycle;Reject(()=>MeleeNative.Joints(cycle),"Cyclic native joint hierarchy is rejected");
        string baseFolder=Path.Combine(output,"base"),modA=Path.Combine(output,"mod-a"),modB=Path.Combine(output,"mod-b");Directory.CreateDirectory(baseFolder);Directory.CreateDirectory(modA);Directory.CreateDirectory(modB);
        File.WriteAllBytes(Path.Combine(baseFolder,"model.dat"),model.Member.CopyOriginalBytes());File.WriteAllBytes(Path.Combine(baseFolder,"motion.dat"),motion.Member.CopyOriginalBytes());
        File.WriteAllBytes(Path.Combine(modA,"model.dat"),Model(9,false).Member.CopyOriginalBytes());File.WriteAllBytes(Path.Combine(modB,"model.dat"),Model(17,false).Member.CopyOriginalBytes());
        using(var a=new MeleeSource(modA,baseFolder))using(var b=new MeleeSource(modB,baseFolder))
        {
            Check(a.Files.Count==2&&b.Files.Count==2,"Partial mod folders inherit missing native files from their explicitly linked base");
            Check(a.Files.Single(f=>f.Path=="motion.dat").FromBase&&!a.Files.Single(f=>f.Path=="model.dat").FromBase,"Overlay provenance distinguishes overridden and inherited members");
            Check(a.FileHash("model.dat")!=b.FileHash("model.dat")&&a.FileHash("motion.dat")==b.FileHash("motion.dat"),"Separate packs with identical asset names retain independent model content");
            Check(a.SourceHash()!=b.SourceHash(),"Effective mod-source fingerprints include exact selected override bytes");
        }
        Reject(()=>{using(var source=new MeleeSource(baseFolder,baseFolder)){}},"A mod cannot use itself as its base");
        using(var cancelled=new CancellationTokenSource()){cancelled.Cancel();Reject(()=>{using(var source=new MeleeSource(baseFolder,null,cancelled.Token)){}},"Cancelled source enumeration publishes no partial provider");}
        var sameA=new MeleeVertex{MatrixIndex=0,NativePosition=System.Numerics.Vector3.One};sameA.Weights.Add(Tuple.Create(1,.25f));sameA.Weights.Add(Tuple.Create(2,.75f));
        var sameB=new MeleeVertex{MatrixIndex=0,NativePosition=System.Numerics.Vector3.One};sameB.Weights.AddRange(sameA.Weights);
        var distinct=new MeleeVertex{MatrixIndex=1,NativePosition=System.Numerics.Vector3.One};distinct.Weights.AddRange(sameA.Weights);
        var witnessMesh=new MeleeMesh{OwnerJoint=0,Vertices=new[]{sameA,sameB,distinct}};
        var witnesses=new MeleeSurfaceWitnesses(new[]{witnessMesh,new MeleeMesh{OwnerJoint=1,Vertices=new[]{sameA}}});
        Check(witnesses.SourceVertices==4&&witnesses.EquationCount==3,"Surface verification shares exact duplicate positions only within the same native mesh and matrix slot");
        sameB.Weights.Reverse();Reject(()=>new MeleeSurfaceWitnesses(new[]{witnessMesh}),"Surface witness sharing rejects changed ordered skin influences");
        var policyRoot=Joint();var policyTail=policyRoot;for(int i=1;i<6;i++){policyTail.Child=Joint();policyTail=policyTail.Child;}
        var policyFile=new HSDRawFile();policyFile.Roots.Add(new HSDRootNode{Name="PlyRenamed_Share_joint",Data=policyRoot});
        var policyModel=MeleeNative.ReadMembers(Bytes(policyFile)).SelectMany(MeleeNative.Models).Single();
        HSDRaw.Melee.SBM_BoneLookupTable Table(int body)
        {
            var table=new HSDRaw.Melee.SBM_BoneLookupTable();var attributes=new HSDAccessor();attributes._s.SetData(Enumerable.Repeat((byte)255,64).ToArray());table._s.SetReference(4,attributes);
            table.Top=0;table.Trans=1;table.XRot=2;table.YRot=3;table.Hip=(byte)body;
            // Native parts_num includes unnamed joints; accessor setters rebuild
            // it from named roles, so restore the fixture's actual native count.
            table.BoneCount=6;return table;
        }
        var commonRoles=new HSDRaw.Melee.SBM_ftLoadCommonData();commonRoles._s.SetData(new byte[0x5c]);
        commonRoles.BoneTables=new HSDFixedLengthPointerArrayAccessor<HSDRaw.Melee.SBM_BoneLookupTable>{Array=new[]{Table(4)}};
        var editPolicy=MeleeBoneEditPolicy.Create(policyModel,commonRoles);
        Check(editPolicy.Available&&editPolicy.BodyJoint==4&&editPolicy.CanEdit(4)&&editPolicy.CanEdit(5),"Native semantic tables authorize existing body descendants without character or bone-name rules");
        Check(editPolicy.ProtectedJoints.SequenceEqual(new[]{0,1,2,3})&&!editPolicy.CanEdit(0),"Native semantic authorization protects every original body ancestor");
        commonRoles.BoneTables.Array=new[]{Table(4),Table(5)};Check(!MeleeBoneEditPolicy.Create(policyModel,commonRoles).Available,"Conflicting native semantic tables require review instead of guessing a body or root role");
        Check(!MeleeBoneEditPolicy.Create(policyModel,null).Available,"Missing native semantic context never opens all bones for editing");
        foreach(int influences in new[]{5,9,17})
        {
            var weighted=Model(influences,true);var projection=MeleeProjection.Write(weighted,Path.Combine(output,"weights-"+influences));
            Check(projection.Meshes.SelectMany(m=>m.Vertices).All(v=>v.Weights.Count==influences),"Native mesh retains every influence for "+influences+"-weight vertices");
            Check(projection.Meshes.SelectMany(m=>m.Vertices).SelectMany(v=>v.Weights).All(w=>w.Item2==1f/influences),"Native skin weights remain unrounded for "+influences+"-influence fixture");
            foreach(var j in MeleeNative.Joints(weighted.Root).Skip(1))j.InverseWorldTransform=new HSD_Matrix4x3{M11=1,M22=1,M33=1};
            var weightedPose=new MeleePose(weighted);float expectedOffset=(influences-1)*.125f;
            Check(projection.Meshes.All(mesh=>mesh.Vertices.All(v=>Math.Abs(System.Numerics.Vector3.Transform(v.NativePosition,weightedPose.VertexMatrix(mesh,v)).X-v.NativePosition.X-expectedOffset)<.00001)),"Native deformation independently includes all "+influences+" influences, without normalization or a four-weight limit");
        }
        Console.WriteLine("PASS "+checks+" native Melee procedural checks");
    }
}
