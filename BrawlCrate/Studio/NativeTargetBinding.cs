using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
namespace BrawlCrate.Studio
{
    // Format-neutral project ownership. Adapters retain native inputs separately
    // from exact editable masters; a display conversion is never a game export.
    public sealed class NativeTargetBinding
    {
        public string Provider {get;set;}
        public string ModelSource {get;set;}
        public string MotionSource {get;set;}
        public string ContextSource {get;set;}
        public string CommonSource {get;set;}
        public string ExecutableSource {get;set;}
        public string AnimationBaselineSource {get;set;}
        public string AnimationMasterId {get;set;}
        public string ModelKey {get;set;}
        public string MotionKey {get;set;}
        public string MotionSnapshotKey {get;set;}
        public string OriginMotionContainerSha256 {get;set;}
        public string SourceRig {get;set;}
        public string NativeModelSha256 {get;set;}
        public string NativeMotionSha256 {get;set;}
        public string OriginContainerSha256 {get;set;}
        public string OriginModelPath {get;set;}
        public string OriginMotionPath {get;set;}
        public string AuthoringModelSha256 {get;set;}
        public string AuthoringAnimationSha256 {get;set;}
        public int CostumeIndex {get;set;}=-1;
        public int[] PartChoices {get;set;}
        internal static string DependencyKey(FighterProject project)=>new System.Web.Script.Serialization.JavaScriptSerializer{MaxJsonLength=1024*1024}.Serialize(new
        {
            Binding=project.NativeTarget,
            Sources=project.NativeSources.OrderBy(a=>a.Id,StringComparer.Ordinal).Select(a=>new{a.Id,a.ContentSha256,a.WorkingPath}).ToArray()
        });
        public void Validate(FighterProject project)
        {
            if(string.IsNullOrWhiteSpace(Provider)||Provider.Length>128||string.IsNullOrEmpty(ModelKey)||ModelKey.Length>1024||string.IsNullOrEmpty(MotionKey)||MotionKey.Length>1024||string.IsNullOrEmpty(MotionSnapshotKey)||MotionSnapshotKey.Length>1024||PartChoices==null||PartChoices.Length>128||PartChoices.Any(v=>v< -1||v>255)||CostumeIndex< -1||CostumeIndex>255)throw new InvalidDataException("Invalid native target binding.");
            foreach(string hash in new[]{SourceRig,NativeModelSha256,NativeMotionSha256,OriginContainerSha256,OriginMotionContainerSha256,AuthoringModelSha256,AuthoringAnimationSha256})if(hash==null||hash.Length!=64||hash.Any(c=>!Uri.IsHexDigit(c)))throw new InvalidDataException("Invalid native target content identity.");
            foreach(string id in new[]{ModelSource,MotionSource,ContextSource,CommonSource,ExecutableSource,AnimationBaselineSource}.Where(s=>s!=null))if(!project.NativeSources.Any(a=>a.Id==id))throw new InvalidDataException("Native target lost an owned source snapshot.");
            if(string.IsNullOrEmpty(AnimationMasterId)||AnimationMasterId.Length!=32||AnimationMasterId.Any(c=>!Uri.IsHexDigit(c)))throw new InvalidDataException("Invalid native animation member association.");
            if(ModelSource==null||MotionSource==null||AnimationBaselineSource==null||project.Model==null||project.Animations.Count==0)throw new InvalidDataException("Native target requires model and animation masters and native originals.");
            foreach(string path in new[]{OriginModelPath,OriginMotionPath})if(path!=null&&path.Length>32767)throw new InvalidDataException("Native target source provenance exceeds its bound.");
        }
    }
}
