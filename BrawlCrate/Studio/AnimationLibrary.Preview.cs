using BrawlLib.SSBB.ResourceNodes;
using System;
using System.IO;
using System.Linq;

namespace BrawlCrate.Studio
{
    public sealed partial class AnimationLibrary
    {
        private LibrarySourceState PreviewState(LibraryCatalogue data,LibraryAnimation clip,bool force=false)
        {
            if(!LibraryNativePreview.Supported(clip))return LibrarySourceState.UnsupportedChannelVersion;
            var motion=data.Mounts.Single(m=>m.Id==clip.MountId);
            if(motion.Provider!=BrawlLibrarySource.Provider)return LibrarySourceState.UnsupportedChannelVersion;
            var state=MountState(motion,force);
            if(state!=LibrarySourceState.Available)return state;
            var anchor=LibraryNativePreview.Anchor(data,clip);
            if(anchor?.ModelId==null)return LibrarySourceState.NeedsModelAssociation;
            var model=data.Models.Single(m=>m.Id==anchor.ModelId);
            return MountState(data.Mounts.Single(m=>m.Id==model.MountId),force);
        }
        public LibrarySourceState GetPreviewState(string clipId)
        {lock(cacheSync){var data=SearchSnapshot();var clip=data.Clips.SingleOrDefault(c=>c.Id==clipId);return clip==null?LibrarySourceState.Missing:PreviewState(data,clip);}}
        public LibraryCostumeOption[] GetPreviewCostumes(string clipId)
        {
            lock(cacheSync)
            {
                var data=SearchSnapshot();var clip=data.Clips.SingleOrDefault(c=>c.Id==clipId);
                var anchor=clip==null?null:LibraryNativePreview.Anchor(data,clip);
                return anchor==null?new LibraryCostumeOption[0]:GetCostumes(anchor.Id);
            }
        }
        public bool TrySelectPreview(LibrarySourceLease lease,string clipId,string modelId)
        {
            if(lease?.Reference==null||lease.LibraryDirectory!=DirectoryPath||lease.ModelRoot==null||lease.MotionRoot==null||lease.Reference.ModelId!=modelId)return false;
            lock(cacheSync)
            {
                var data=SearchSnapshot();var clip=data.Clips.SingleOrDefault(c=>c.Id==clipId);
                if(data.Generation!=lease.CatalogueGeneration||clip==null||clip.MountId!=lease.PreviewMotionMount||PreviewState(data,clip)!=LibrarySourceState.Available||
                    !GetPreviewCostumes(clipId).Any(c=>c.ModelId==modelId&&c.Available))return false;
                var animation=BrawlLibrarySource.Resolve(lease.MotionRoot,clip.ResourceKey) as NW4RAnimationNode;
                if(!LibraryNativePreview.MatchesChannel(animation,clip.Channel)||animation.Name!=clip.Name||animation.Version!=clip.NativeVersion||animation.FrameCount!=clip.FrameCount||animation.Loop!=clip.Loop||
                    BrawlLibrarySource.ResourceHash(animation)!=clip.ResourceSha256)throw new InvalidDataException("The prepared native source differs from the selected resource.");
                var reference=lease.Reference.Copy();reference.ClipId=clip.Id;reference.Revision=clip.Revision;reference.MotionKey=clip.ResourceKey;
                reference.MotionResourceSha256=clip.ResourceSha256;reference.NativeVersion=clip.NativeVersion;reference.FrameCount=clip.FrameCount;
                reference.Loop=clip.Loop;reference.TicksPerSecond=clip.TicksPerSecond;reference.TimebaseEvidence=clip.TimebaseEvidence;reference.Channel=clip.Channel;
                reference.UnboundSourceTracks=clip.UnboundSourceTracks;
                reference.AssociationEvidence=clip.Channel!="CHR0 skeletal"?"Read-only native channel preview on the selected source model; not a skeletal retarget association":
                    modelId==clip.ModelId?clip.AssociationEvidence:BrawlLibraryCostumes.AssociationEvidence(clip,data.Models.Single(m=>m.Id==modelId));
                lease.Model.ResetToBindState();lease.Model.ApplySHP(null,0);
                lease.Reference=reference;lease.Animation=animation as CHR0Node;lease.PreviewAnimation=animation;
                lease.PreviewBundle=LibraryNativePreview.Bundle(lease.MotionRoot,animation,out lease.PreviewNotice);return true;
            }
        }
    }
}
