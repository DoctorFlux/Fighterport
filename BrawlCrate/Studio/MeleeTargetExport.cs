using FighterPort.Adapters.Melee;
using BrawlLib.SSBB.ResourceNodes;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
namespace BrawlCrate.Studio
{
    internal static class MeleeTargetExport
    {
        internal static MeleeWriteCandidate Prepare(FighterProject project,MDL0Node target,CHR0Node clip,CancellationToken cancellation,out NativeTargetExportReceipt receipt)
        {
            MeleeTargetDocument.Validate(project);
            if(target==null||clip==null)throw new InvalidDataException("Select the owned target animation.");
            if(project.Model.ContentSha256!=project.NativeTarget.AuthoringModelSha256||target.IsDirty)
                throw new NotSupportedException("Native model edits need their own verified HSD writer before this motion can be exported for that changed rig. The editable project is preserved.");
            var native=MeleeTargetDocument.OriginalModel(project);var source=MeleeTargetDocument.OriginalMotion(project);
            using(var baseline=NodeFactory.FromFile(null,project.NativeSources.Single(a=>a.Id==project.NativeTarget.AnimationBaselineSource).WorkingPath) as CHR0Node)
            {
                if(baseline==null)throw new InvalidDataException("Owned native authoring baseline is unavailable.");
                if(clip.Loop!=baseline.Loop)throw new NotSupportedException("Melee runtime looping is owned by the action context. A CHR0 preview-loop change is not silently written as a game rule.");
                var names=new HashSet<string>(Enumerable.Range(0,MeleeNative.Joints(native.Root).Length).Select(MeleeProjection.JointName));
                var entries=clip.Children.Cast<CHR0EntryNode>().ToArray();
                if(entries.Any(e=>!names.Contains(e.Name))||entries.Select(e=>e.Name).Distinct().Count()!=entries.Length)throw new InvalidDataException("Native export needs exact target-joint bindings; an animation entry is unmatched or ambiguous.");
                var changes=new List<MeleeCurveEdit>();
                for(int joint=0;joint<names.Count;joint++)for(int axis=0;axis<9;axis++)
                {
                    cancellation.ThrowIfCancellationRequested();string name=MeleeProjection.JointName(joint);
                    var current=entries.SingleOrDefault(e=>e.Name==name)?.KeyArrays[axis];
                    var original=baseline.Children.OfType<CHR0EntryNode>().SingleOrDefault(e=>e.Name==name)?.KeyArrays[axis];
                    var a=current?.Entries.ToArray();var b=original?.Entries.ToArray();
                    if((a?.Length??0)==(b?.Length??0)&&(a==null||a.Length==0||a.Zip(b,(x,y)=>x._index==y._index&&Bits(x._value)==Bits(y._value)&&Bits(x._tangent)==Bits(y._tangent)).All(equal=>equal)))continue;
                    var keys=new List<MeleeCurveKey>();double factor=axis>=3&&axis<6?Math.PI/180:1;
                    if(a!=null)foreach(var group in a.GroupBy(k=>k._index))
                    {
                        var sides=group.ToArray();if(sides.Length>2||sides.Any(k=>Bits(k._value)!=Bits(sides[0]._value)))throw new NotSupportedException("A native discontinuity needs an explicitly qualified FOBJ boundary representation. No key was discarded.");
                        keys.Add(new MeleeCurveKey{Frame=group.Key,Value=(float)(sides[0]._value*factor),Incoming=(float)(sides[0]._tangent*factor),Outgoing=(float)(sides.Last()._tangent*factor)});
                    }
                    if(keys.Count>0&&keys[0].Frame>0)keys.Insert(0,new MeleeCurveKey{Frame=0,Value=keys[0].Value});
                    changes.Add(new MeleeCurveEdit{Joint=joint,Axis=axis,Keys=keys.ToArray()});
                }
                float last=clip.FrameCount==baseline.FrameCount?source.LastFrame:clip.FrameCount-1;
                var candidate=MeleeMotionWriter.Prepare(source,changes,last,cancellation,names.Count);
                var decoded=MeleeNative.ReadMembers(candidate.CopyBytes(),cancellation).SelectMany(MeleeNative.Motions).Single(m=>m.Symbol==source.Symbol);
                receipt=Verify(native,source,decoded,clip,last,cancellation);receipt.Provider=project.NativeTarget.Provider;receipt.ResourceKey=project.NativeTarget.MotionKey;receipt.Sha256=candidate.Sha256;receipt.Bytes=candidate.Length;return candidate;
            }
        }
        private static int Bits(float value)=>BitConverter.ToInt32(BitConverter.GetBytes(value),0);
        private static NativeTargetExportReceipt Verify(MeleeNativeModel model,MeleeNativeMotion source,MeleeNativeMotion saved,CHR0Node master,float last,CancellationToken cancellation)
        {
            var expectedPlayback=new MeleePlayback(model,source);var actual=new MeleePlayback(model,saved);var nativeBones=MeleeNative.Joints(model.Root);
            if(saved.LastFrame!=last||actual.JointCount!=nativeBones.Length)throw new InvalidDataException("Native export changed duration or joint ownership.");
            var entries=master.Children.OfType<CHR0EntryNode>().ToDictionary(e=>e.Name);
            var times=new SortedSet<float>{0,last};
            foreach(var e in entries.Values)foreach(var array in e.KeyArrays)foreach(var key in array.Entries)if(key._index>=0&&key._index<=last)times.Add(key._index);
            long count=(long)Math.Ceiling(last*8)+1;if(count*nativeBones.Length>2000000)throw new InvalidDataException("Native motion verification exceeds the two-million-sample budget.");
            for(int i=0;i<count;i++)times.Add(Math.Min(last,i/8f));
            var points=times.ToArray();for(int i=1;i<points.Length;i++)times.Add(points[i-1]+(points[i]-points[i-1])*.381966f);
            if((long)times.Count*nativeBones.Length>2000000)throw new InvalidDataException("Native motion verification exceeds its complete sample budget.");
            var geometry=MeleeProjection.Prepare(model).Meshes;var witnesses=new MeleeSurfaceWitnesses(geometry);var expectedPose=new MeleePose(model);var decodedPose=new MeleePose(model);
            var values=nativeBones.Select(j=>new float[9]).ToArray();var classical=new bool[nativeBones.Length];
            var bindPoints=witnesses.Slots.SelectMany(slot=>slot.Vertices.Select(v=>System.Numerics.Vector3.Transform(v.NativePosition,expectedPose.VertexMatrix(slot.Mesh,v)))).ToArray();
            if(bindPoints.Length==0)throw new InvalidDataException("Native target surface verification has no geometry.");
            var min=bindPoints.Aggregate(System.Numerics.Vector3.Min);var max=bindPoints.Aggregate(System.Numerics.Vector3.Max);
            double scale=System.Numerics.Vector3.Distance(min,max);if(!(scale>0)||double.IsInfinity(scale))throw new InvalidDataException("Native target has no finite positive surface scale.");
            if((long)times.Count*bindPoints.Length>16000000)throw new InvalidDataException("Native surface verification exceeds its sixteen-million-vertex budget.");
            double positionLimit=.0001*scale;var receipt=new NativeTargetExportReceipt{SampleTimes=times.Count,NativeVertices=witnesses.SourceVertices,VerifiedVertexEquations=witnesses.EquationCount,ScaleReference=scale};
            foreach(float time in times)
            {
                cancellation.ThrowIfCancellationRequested();
                for(int joint=0;joint<nativeBones.Length;joint++)
                {
                    var j=nativeBones[joint];float[] expected={j.SX,j.SY,j.SZ,j.RX,j.RY,j.RZ,j.TX,j.TY,j.TZ};
                    entries.TryGetValue(MeleeProjection.JointName(joint),out var entry);
                    if(entry!=null)for(int axis=0;axis<9;axis++)if(entry.KeyArrays[axis].KeyCount>0)expected[axis]=entry.KeyArrays[axis].GetFrameValue(time)*(axis>=3&&axis<6?(float)(Math.PI/180):1);
                    values[joint]=expected;
                    bool authored=entry!=null&&entry.KeyArrays.Any(a=>a.KeyCount>0);
                    classical[joint]=authored&&source.Figatree!=null?(source.Figatree.Type&1)!=0:expectedPlayback.UsesClassicalScale(joint,(j.Flags&HSDRaw.Common.JOBJ_FLAG.CLASSICAL_SCALING)!=0);
                    var pose=actual.Sample(joint,time);
                    for(int axis=0;axis<9;axis++)
                    {
                        MeleeNative.Finite(expected[axis]);MeleeNative.Finite(pose[axis]);
                        double limit=axis>=3&&axis<6?.0003*Math.PI/180:Math.Max(.000002,Math.Abs(expected[axis])*.000001);
                        double error=Math.Abs(pose[axis]-expected[axis]);
                        if(axis<3)receipt.MaximumLocalScale=Math.Max(receipt.MaximumLocalScale,error);else if(axis<6)receipt.MaximumLocalRotationDegrees=Math.Max(receipt.MaximumLocalRotationDegrees,error*180/Math.PI);else receipt.MaximumLocalTranslation=Math.Max(receipt.MaximumLocalTranslation,error);
                        if(error>limit)throw new InvalidDataException("Fresh native motion differs from its editable master at joint "+joint+", channel "+axis+", frame "+time+"; expected "+expected[axis]+", decoded "+pose[axis]+". Nothing was exported.");
                    }
                }
                expectedPose.SampleAuthored(values,classical);decodedPose.Sample(actual,time);
                for(int joint=0;joint<nativeBones.Length;joint++)
                {
                    var a=expectedPose.World[joint];var b=decodedPose.World[joint];
                    double error=System.Numerics.Vector3.Distance(new System.Numerics.Vector3(a.M41,a.M42,a.M43),new System.Numerics.Vector3(b.M41,b.M42,b.M43));receipt.MaximumWorldPosition=Math.Max(receipt.MaximumWorldPosition,error);
                    if(double.IsNaN(error)||double.IsInfinity(error)||error>positionLimit)throw new InvalidDataException("Fresh native motion changed cumulative joint positions beyond the native bridge bound.");
                }
                foreach(var slot in witnesses.Slots)
                {
                    var expectedMatrix=expectedPose.VertexMatrix(slot.Mesh,slot.Vertices[0]);var decodedMatrix=decodedPose.VertexMatrix(slot.Mesh,slot.Vertices[0]);
                    foreach(var vertex in slot.Vertices)
                    {
                        var a=System.Numerics.Vector3.Transform(vertex.NativePosition,expectedMatrix);var b=System.Numerics.Vector3.Transform(vertex.NativePosition,decodedMatrix);
                        double error=System.Numerics.Vector3.Distance(a,b);receipt.MaximumSurfacePosition=Math.Max(receipt.MaximumSurfacePosition,error);
                        if(double.IsNaN(error)||double.IsInfinity(error)||error>positionLimit)throw new InvalidDataException("Fresh native motion changed the all-influence native surface beyond its bridge bound.");
                    }
                }
            }
            return receipt;
        }
    }
}
