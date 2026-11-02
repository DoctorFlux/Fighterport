using FighterPort.Adapters.Melee;
using BrawlCrate.Studio.Retargeting;
using BrawlLib.Internal;
using BrawlLib.Modeling;
using BrawlLib.Modeling.Collada;
using BrawlLib.SSBB.ResourceNodes;
using System;
using System.IO;
using System.Linq;
using System.Threading;
namespace BrawlCrate.Studio
{
    internal static class MeleeLibraryPreview
    {
        private static readonly object ImportGate=new object();
        private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<MDL0Node,MeleeProjectionReport> Reports=new System.Runtime.CompilerServices.ConditionalWeakTable<MDL0Node,MeleeProjectionReport>();
        internal static MeleeProjectionReport Report(MDL0Node model)=>Reports.GetValue(model,m=>throw new InvalidOperationException("Missing immutable HSD geometry preparation."));
        internal static MDL0Node Project(MeleeNativeModel source,NativeSourceScratch scratch,out string notice,MeleePartVisibility normalDetail=null)
        {
            var report=MeleeProjection.Write(source,scratch.DirectoryPath,true,normalDetail);notice=string.Join(" ",report.Notices.Distinct());
            lock(ImportGate)
            {
                var previous=Collada._importOptions;
                try
                {
                    Collada._importOptions=new Collada.ImportOptions{_blenderBoneFix=false,_rmpMats=false,_rmpClrs=false,_weightPrecision=0,_useOneNode=false,_setOrigPath=false};
                    using(var importer=new Collada())
                    {
                        var result=importer.ImportModel(report.ColladaPath,Collada.ImportType.MDL0,false) as MDL0Node;
                        if(result==null)throw new InvalidDataException("Melee display projection failed: "+Collada.Error);
                        result.Children.ToArray();result.BoneGroup?.Children.ToArray();
                        result.Name=source.Symbol;var original=MeleeNative.Joints(source.Root);string rigHash=MeleeNative.RigHash(source);var bones=result.AllBones.ToArray();
                        if(original.Length!=bones.Length||bones.Where((b,i)=>b.Name!=MeleeProjection.JointName(i)).Any())
                        {result.Dispose();throw new InvalidDataException("Display projection changed the native joint identity/order.");}
                        for(int i=0;i<bones.Length;i++)
                        {
                            var j=original[i];var b=bones[i].BindState;
                            NativeJointIdentity.Stamp(bones[i],MeleeGameSource.NativeProvider,rigHash,i);
                            bones[i].ClassicScale=(j.Flags&HSDRaw.Common.JOBJ_FLAG.CLASSICAL_SCALING)!=0;
                            bones[i].BindState=bones[i].FrameState=new FrameState(new Vector3(j.SX,j.SY,j.SZ),new Vector3(j.RX,j.RY,j.RZ)*(180f/(float)Math.PI),new Vector3(j.TX,j.TY,j.TZ));
                            if((b.Scale-new Vector3(j.SX,j.SY,j.SZ)).TrueDistance(new Vector3())>0.0001f||(b.Translate-new Vector3(j.TX,j.TY,j.TZ)).TrueDistance(new Vector3())>0.0001f)
                            {result.Dispose();throw new InvalidDataException("Display projection changed native bind transforms at joint "+i+"; scale "+b.Scale+" vs "+j.SX+","+j.SY+","+j.SZ+"; translation "+b.Translate+" vs "+j.TX+","+j.TY+","+j.TZ);}
                        }
                        // Finish the ordinary native write/read boundary before viewport framing.
                        // The importer retains construction-time buffers until serialization;
                        // GetBox on those buffers can otherwise be empty before the first draw.
                        string nativePath=Path.Combine(scratch.DirectoryPath,"MeleeDisplay.mdl0");
                        result.Export(nativePath);result.Dispose();
                        var reopened=NodeFactory.FromFile(null,nativePath) as MDL0Node;
                        if(reopened==null)throw new InvalidDataException("Native Melee display projection did not reopen.");
                        reopened.Children.ToArray();reopened.BoneGroup?.Children.ToArray();reopened.AllBones.ToArray();
                        NativeJointIdentity.Bind(reopened,MeleeGameSource.NativeProvider,rigHash,original.Length);
                        Reports.Add(reopened,report);return reopened;
                    }
                }
                finally{Collada._importOptions=previous;}
            }
        }
        internal static void Apply(LibrarySourceLease lease,float displayFrame)
        {
            var bones=lease.MeleeBones??(lease.MeleeBones=lease.Model.AllBones.ToArray());
            if(lease.MeleePose==null)lease.MeleePose=bones.Select(b=>new float[9]).ToArray();float nativeFrame=displayFrame<=0?-1:Math.Min(lease.MeleePlayback.LastFrame,displayFrame-1);
            for(int i=0;i<bones.Length;i++)
            {
                var value=lease.MeleePose[i];lease.MeleePlayback.SampleInto(i,nativeFrame,value);bones[i].FrameState=new FrameState(new Vector3(value[0],value[1],value[2]),new Vector3(value[3],value[4],value[5])*(180f/(float)Math.PI),new Vector3(value[6],value[7],value[8]));
            }
            if(lease.MeleeRenderer==null)lease.Model.WeightMeshes();else{foreach(var root in bones.Where(b=>!(b.Parent is MDL0BoneNode)))root.RecalcFrameState();lease.MeleeRenderer.UpdatePose(lease.MeleePlayback,nativeFrame);}
        }
    }
    internal sealed class MeleePreparedPreview:INativeSourcePreview
    {
        private readonly LibrarySourceLease lease;
        public BrawlLib.OpenGL.IRenderedObject RenderTarget=>lease.MeleeRenderer;
        public string AnimationName {get;private set;}
        public int FrameCount {get;private set;}
        public bool Loop=>false; // Melee action-script loop policy is not inferred.
        internal MeleePreparedPreview(LibrarySourceLease lease,string name,int frames){this.lease=lease;Select(name,frames);}
        internal void Select(string name,int frames){if(string.IsNullOrEmpty(name)||frames<1)throw new InvalidDataException("Invalid native preview timing.");AnimationName=name;FrameCount=frames;}
        public void ApplyDisplayFrame(float frame)=>MeleeLibraryPreview.Apply(lease,frame);
        public void Dispose()=>lease.MeleeRenderer?.Dispose();
    }
    public sealed partial class AnimationLibrary
    {
        private LibrarySourceLease OpenMelee(LibraryCatalogue data,LibraryAnimation clip,string modelId,CancellationToken cancellation,bool preview)
        {
            if(State(data,clip,true)!=LibrarySourceState.Available)throw new InvalidDataException("The exact Melee source or model association is unavailable.");
            var choices=MeleeGameSource.Costumes(data,clip);string chosen=modelId??clip.ModelId;
            if(!choices.Any(c=>c.ModelId==chosen&&c.Available))throw new InvalidDataException("Choose a verified same-source Melee costume with the exact associated rig.");
            var model=data.Models.Single(m=>m.Id==chosen);var a=data.Mounts.Single(m=>m.Id==model.MountId);var b=data.Mounts.Single(m=>m.Id==clip.MountId);
            var lease=new LibrarySourceLease{Scratch=new NativeSourceScratch(),LibraryDirectory=DirectoryPath,CatalogueGeneration=data.Generation,PreviewMotionMount=b.Id,ProvenanceModelPath=a.OriginPath??a.LocalPath};
            try
            {
                foreach(string path in new[]{a.OriginPath??a.LocalPath,b.OriginPath??b.LocalPath}.Distinct(StringComparer.OrdinalIgnoreCase)){MeleeSource.NoLinks(path);var held=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.Read);if(held.Length>4L*1024*1024*1024){held.Dispose();throw new InvalidDataException("Melee source exceeds the reader bound.");}lease.OriginLocks.Add(held);}
                byte[] modelBytes=MeleeGameSource.Read(a,cancellation),motionBytes=MeleeGameSource.Read(b,cancellation);
                lease.MeleeModel=MeleeNative.ReadMembers(modelBytes,cancellation).SelectMany(MeleeNative.Models).Single(m=>m.Key==model.ResourceKey&&m.Member.Sha256==model.ResourceSha256);
                if(MeleeNative.RigHash(lease.MeleeModel)!=model.SkeletonSha256)throw new InvalidDataException("The exact native Melee rig changed.");
                lease.MeleeMotionMembers=MeleeNative.ReadMembers(motionBytes,cancellation).ToArray();
                var motion=lease.MeleeMotionMembers.SelectMany(MeleeNative.Motions).Single(m=>m.Key==clip.ResourceKey&&m.Member.Sha256==clip.ResourceSha256);
                lease.MeleePlayback=new MeleePlayback(lease.MeleeModel,motion);
                string partNotice;var parts=MeleeGameSource.PrepareParts(data,clip,model,lease.MeleeModel,MeleeProjection.DisplayObjectCount(lease.MeleeModel),lease.OriginLocks,cancellation,out partNotice);
                if(parts==null)throw new InvalidDataException("Normal-detail model visibility could not be verified. No LOD or all-mesh fallback is used. "+partNotice);
                lease.Model=MeleeLibraryPreview.Project(lease.MeleeModel,lease.Scratch,out lease.PreviewNotice,parts);lease.ModelRoot=lease.Model;lease.ModelPath=lease.Scratch.DirectoryPath;
                lease.MeleeRenderer=new MeleeNativeRender(lease.Model,MeleeLibraryPreview.Report(lease.Model),lease.MeleeModel);lease.NativePreview=new MeleePreparedPreview(lease,clip.Name,clip.FrameCount);
                lease.MeleeRenderer.Parts=parts;lease.MeleeRenderer.RefreshParts();
                lease.PreviewNotice="Normal-detail native HSD preview. "+partNotice+" Material/shape animation and gameplay visibility are not simulated.";
                lease.NativePreviewOnly=preview;
                if(!preview)
                {
                    var master=MeleeAnimationMaster.Prepare(lease.MeleeModel,motion,clip.Name);
                    lease.Animation=master;lease.PreviewAnimation=master;lease.MotionRoot=master;
                }
                lease.PreviewBundle=new NW4RAnimationNode[0];
                var common=data.Mounts.SingleOrDefault(m=>m.GameSourceId==b.GameSourceId&&string.Equals(System.IO.Path.GetFileName(m.VirtualPath),"PlCo.dat",StringComparison.OrdinalIgnoreCase));
                lease.Model.NativeEditingBackend=new MeleeModelEditingBackend(lease.Model,lease.MeleeModel,motion,parts,common==null?null:MeleeGameSource.Read(common,cancellation),lease.MeleeRenderer);
                lease.Reference=new LibraryClipReference{ClipId=clip.Id,Revision=clip.Revision,ModelId=model.Id,Game="Melee",GameVersion=a.GameVersion,Platform="GameCube",BuildEvidence=a.BuildEvidence,Adapter=MeleeGameSource.NativeProvider,ModelMountId=a.Id,MotionMountId=b.Id,ModelKey=model.ResourceKey,MotionKey=clip.ResourceKey,ModelContainerSha256=a.ContainerSha256,MotionContainerSha256=b.ContainerSha256,ModelResourceSha256=model.ResourceSha256,MotionResourceSha256=clip.ResourceSha256,SourceRig=model.SkeletonSha256,RestInterpretation=model.RestInterpretation,AssociationEvidence=clip.AssociationEvidence,NativeVersion=1,FrameCount=clip.FrameCount,Loop=false,TicksPerSecond=60,TimebaseEvidence=clip.TimebaseEvidence,Channel=MeleeGameSource.Channel,GameSourceId=a.GameSourceId,GameSourceSha256=data.GameLocations.Single(l=>l.Id==a.GameSourceId).ContainerSha256,SourceProvider=data.GameLocations.Single(l=>l.Id==a.GameSourceId).Provider,ModelVirtualPath=a.VirtualPath,MotionVirtualPath=b.VirtualPath};
                cancellation.ThrowIfCancellationRequested();return lease;
            }
            catch{lease.Dispose();throw;}
        }
        private bool TrySelectMeleePreview(LibrarySourceLease lease,string clipId,string modelId)
        {
            if(lease.Reference.ModelId!=modelId||lease.LibraryDirectory!=DirectoryPath)return false;
            lock(cacheSync)
            {
                var data=SearchSnapshot();var clip=data.Clips.SingleOrDefault(c=>c.Id==clipId);
                if(clip==null||data.Generation!=lease.CatalogueGeneration||clip.MountId!=lease.PreviewMotionMount||clip.ModelId!=lease.Reference.ModelId&&!MeleeGameSource.Costumes(data,clip).Any(c=>c.ModelId==modelId)||State(data,clip)!=LibrarySourceState.Available)return false;
                var motion=lease.MeleeMotionMembers.SelectMany(MeleeNative.Motions).Single(m=>m.Key==clip.ResourceKey&&m.Member.Sha256==clip.ResourceSha256);
                var playback=new MeleePlayback(lease.MeleeModel,motion);var reference=lease.Reference.Copy();reference.ClipId=clip.Id;reference.Revision=clip.Revision;reference.MotionKey=clip.ResourceKey;reference.MotionResourceSha256=clip.ResourceSha256;reference.FrameCount=clip.FrameCount;reference.TimebaseEvidence=clip.TimebaseEvidence;
                CHR0Node master=null;
                if(!lease.NativePreviewOnly)master=MeleeAnimationMaster.Prepare(lease.MeleeModel,motion,clip.Name);
                lease.MotionRoot?.Dispose();lease.Animation=master;lease.PreviewAnimation=master;lease.MotionRoot=master;
                ((MeleePreparedPreview)lease.NativePreview).Select(clip.Name,clip.FrameCount);
                lease.MeleePlayback=playback;lease.Reference=reference;return true;
            }
        }
    }
}
