using BrawlCrate.Studio;
using BrawlLib.SSBB.ResourceNodes;
using FighterPort.Adapters.Melee;
using HSDRaw;
using HSDRaw.Common;
using HSDRaw.Common.Animation;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
internal static partial class Program
{
    static void MeleeMasterBridgeChecks(string output)
    {
        var policies=new[]{new FighterPort.NativeEvaluation.JointPolicy{SkeletonRoot=true,HasInverseBind=true,InverseBind=System.Numerics.Matrix4x4.Identity},new FighterPort.NativeEvaluation.JointPolicy{Skeleton=true,HasInverseBind=true,InverseBind=System.Numerics.Matrix4x4.CreateTranslation(-2,0,0)}};
        var owner=new FighterPort.NativeEvaluation.SkinPolicy{Owner=0,Envelope=true};
        var parents=new[]{-1,0};var worlds=new[]{System.Numerics.Matrix4x4.Identity,System.Numerics.Matrix4x4.CreateTranslation(3,0,0)};
        var point=new System.Numerics.Vector3(4,1,0);var bones=new[]{0,1};var weights=new[]{.25f,.75f};
        var exact=System.Numerics.Vector3.Transform(point,FighterPort.NativeEvaluation.EvaluationMath.SkinMatrix(policies,parents,worlds,owner,bones,weights));
        var prepared=FighterPort.NativeEvaluation.EvaluationMath.StaticWeights(policies,owner,point,bones,weights);
        var staticResult=prepared.Aggregate(System.Numerics.Vector3.Zero,(sum,i)=>sum+System.Numerics.Vector3.Transform(i.Local,worlds[i.Joint])*i.Weight);
        Check(System.Numerics.Vector3.Distance(exact,staticResult)<1e-6,"Prepared static skin equations match the complete native evaluator without discarding influence weights");
        var dynamicPolicies=new[]{policies[0],policies[1],new FighterPort.NativeEvaluation.JointPolicy{HasInverseBind=true,InverseBind=System.Numerics.Matrix4x4.Identity}};
        Check(FighterPort.NativeEvaluation.EvaluationMath.StaticWeights(dynamicPolicies,new FighterPort.NativeEvaluation.SkinPolicy{Owner=2,Envelope=true},point,bones,weights)==null,"Moving native owner basis retains the general skin evaluator rather than a static approximation");
        byte[] Serialize(HSDRawFile file){using(var stream=new MemoryStream()){file.Save(stream,bufferAlign:false,optimize:false,trim:false);return stream.ToArray();}}
        var root=new HSD_JOBJ{SX=2,SY=3,SZ=-4,RY=9.25f,RZ=-.6f,TX=5,TY=2,TZ=-3};
        root.Child=new HSD_JOBJ{SX=1,SY=1,SZ=1,RY=1.7f,TX=3};
        var modelFile=new HSDRawFile();modelFile.Roots.Add(new HSDRootNode{Name="PlyProcedural_Share_joint",Data=root});
        var model=MeleeNative.ReadMembers(Serialize(modelFile)).SelectMany(MeleeNative.Models).Single();
        var rx=MeleeMotionWriter.Encode(3,new[]{new MeleeCurveKey{Frame=0,Value=0,Incoming=.1f,Outgoing=.1f},new MeleeCurveKey{Frame=4,Value=.4f,Incoming=.1f,Outgoing=.1f}});
        var tx=MeleeMotionWriter.Encode(6,new[]{new MeleeCurveKey{Frame=0,Value=5,Incoming=1,Outgoing=1},new MeleeCurveKey{Frame=4,Value=9,Incoming=1,Outgoing=1}});
        var fig=new HSD_FigaTree{Type=1,FrameCount=4,Nodes=new List<FigaTreeNode>{new FigaTreeNode{Tracks=new List<HSD_Track>{rx,tx}}}};
        var motionFile=new HSDRawFile();motionFile.Roots.Add(new HSDRootNode{Name="PlyProcedural_Share_ACTION_Test_figatree",Data=fig});
        var motion=MeleeNative.ReadMembers(Serialize(motionFile)).SelectMany(MeleeNative.Motions).Single();
        string rig=MeleeNative.RigHash(model),hash=motion.Member.Sha256,path=Path.Combine(output,"procedural-native-master.chr0");
        using(var master=MeleeAnimationMaster.Prepare(model,motion,"Procedural"))master.ExportChecked(path);
        using(var saved=NodeFactory.FromFile(null,path) as CHR0Node)
        {
            MeleeAnimationMaster.VerifySerialized(model,motion,saved);
            var entry=saved.Children.OfType<CHR0EntryNode>().Single();
            Check(Math.Abs(entry.KeyArrays[4].GetFrameValue(2)-9.25*180/Math.PI)<.0001,"Serialized native authoring bridge retains unanimated raw Euler bind values in an enabled rotation group");
            Check(entry.KeyArrays[7].GetFrameValue(2)==2&&entry.KeyArrays[8].GetFrameValue(2)==-3,"Serialized native authoring bridge retains sparse translation bind fallback");
            Check(entry.KeyArrays[6].GetFrameValue(2)==7,"Serialized native authoring bridge independently retains authored root travel");
            Check(saved.Children.Count==1&&!saved.Children.Any(e=>e.Name==MeleeProjection.JointName(1)),"Native authoring creates no track for an unanimated existing helper joint");
            Check(saved.FrameCount==5&&!saved.Loop,"Native end-coordinate four maps to five explicit display frames without inventing runtime looping");
            float original=entry.KeyArrays[3].Entries.First()._value;entry.KeyArrays[3].Entries.First()._value=float.NaN;
            bool rejected=false;try{MeleeAnimationMaster.VerifySerialized(model,motion,saved);}catch(InvalidDataException){rejected=true;}
            Check(rejected,"Native authoring verification rejects nonfinite current curve values");entry.KeyArrays[3].Entries.First()._value=original;
        }
        fig.FrameCount=10000000;var longMotion=MeleeNative.ReadMembers(Serialize(motionFile)).SelectMany(MeleeNative.Motions).Single();bool budgetRejected=false;
        try{using(var oversized=MeleeAnimationMaster.Prepare(model,longMotion,"TooLong")) {}}catch(InvalidDataException){budgetRejected=true;}
        Check(budgetRejected,"Native authoring rejects excessive validation work before allocating its sample schedule");
        Check(MeleeNative.RigHash(model)==rig&&MeleeSource.Sha256(motion.Member.CopyOriginalBytes())==hash,"Preparing and serializing native masters changes no original rig or animation bytes");
        using(var project=FighterProject.Create("Opaque native snapshot"))
        {
            byte[] opaque={1,3,5,7};var snapshot=project.CreateNativeMaster("native-resource.future","native-source",p=>File.WriteAllBytes(p,opaque));project.NativeSources.Add(snapshot);
            string descriptor=Path.Combine(output,"opaque-native-snapshot.fpstudio");project.Save(descriptor);
            using(var reopened=FighterProject.Open(descriptor))
                Check(File.ReadAllBytes(reopened.NativeSources.Single().WorkingPath).SequenceEqual(opaque),"Shared project storage retains opaque future-format snapshots without a Melee extension or decoder");
        }
    }
}
