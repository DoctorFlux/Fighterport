using BrawlCrate.Studio;
using BrawlCrate.Studio.Retargeting;
using BrawlLib.SSBB;
using BrawlLib.SSBB.ResourceNodes;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace BrawlCrate.UI
{
    internal partial class ModelForm
    {
        private static CHR0Node CopyFittingReference(CHR0Node clip)
        {
            var copy=new CHR0Node{Name=clip.Name,Version=clip.Version,FrameCount=clip.FrameCount,Loop=clip.Loop,OriginalPath=clip.OriginalPath};
            foreach(UserDataClass user in clip.UserEntries)copy.UserEntries.Add(new UserDataClass{Name=user.Name,DataType=user.DataType,Entries=user.Entries.ToArray()});
            NativeControlSnapshot.Capture(clip,clip.Children.Select(c=>c.Name)).Restore(copy);PreciseCHR0EntryNode.Promote(copy);return copy;
        }
        private void AddViewerFittingReferences(List<TransitionClipChoice> choices)
        {
            foreach(var root in modelEditControl1._openedFiles.Where(r=>r!=null).Distinct())
            foreach(var clip in DiscoverFighterAnimations(root).OfType<CHR0Node>())
            {
                if(choices.Any(c=>ReferenceEquals(c.Clip,clip)))continue;
                string id=Guid.NewGuid().ToString("N");var owner=root;
                choices.Add(new TransitionClipChoice{External=true,Asset=new FighterAsset{Id=id,SourcePath=root.FilePath??root.Name},Clip=clip,
                    Key="viewer:"+id,Label="Loaded in model preview · "+Path.GetFileName(root.FilePath??root.Name),
                    Validate=()=>{if(!modelEditControl1._openedFiles.Contains(owner)||!DiscoverFighterAnimations(owner).Any(n=>ReferenceEquals(n,clip)))throw new InvalidOperationException("The loaded reference was removed. Choose it again.");}});
            }
        }
        private HashSet<string> FittingBindingEvidence(List<TransitionClipChoice> choices,MDL0Node model)
        {
            string rig=BrawlRetargeter.SkeletonFingerprint(model);var approved=new HashSet<string>(StringComparer.Ordinal);
            var catalogues=new[]{_library,_sharedSourceLibrary,_sessionSourceLibrary}.Concat(_sessionSourceCatalogues).Where(l=>l!=null).Distinct().ToArray();
            foreach(var library in catalogues)
            {
                LibraryCatalogue data;try{data=library.Read();}catch(Exception error)when(error is IOException||error is InvalidDataException||error is UnauthorizedAccessException){continue;}var models=data.Models.ToDictionary(m=>m.Id);var mounts=data.Mounts.ToDictionary(m=>m.Id);
                var proofs=new HashSet<string>(data.Clips.Where(c=>c.Channel=="CHR0 skeletal"&&c.ModelId!=null&&models[c.ModelId].SkeletonSha256==rig)
                    .Select(c=>mounts[c.MountId].ContainerSha256+"|"+c.ResourceKey+"|"+c.ResourceSha256),StringComparer.Ordinal);
                foreach(var choice in choices.Where(c=>!c.External))
                {
                    string key=BrawlLibrarySource.Resources(choice.Clip.RootNode).Single(p=>ReferenceEquals(p.Item1,choice.Clip)).Item2;
                    if(proofs.Contains(choice.Asset.SourceSha256+"|"+key+"|"+BrawlLibrarySource.ResourceHash(choice.Clip)))approved.Add(choice.Key);
                }
            }
            return approved;
        }
        private bool FittingBindingKnown(TransitionClipChoice selected,TransitionFitOptions options,List<TransitionClipChoice> choices,MDL0Node model,HashSet<string> proof)
        {
            string rig=BrawlRetargeter.SkeletonFingerprint(model);
            bool Known(TransitionClipChoice choice)=>choice!=null&&(choice.Asset.RigIdentity==rig||proof.Contains(choice.Key))&&
                choice.Clip.Children.All(t=>model.AllBones.Count(b=>b.Name==t.Name)==1);
            return Known(selected)&&(options.Entry==null||Known(choices.SingleOrDefault(c=>c.Key==options.Entry)))&&
                (options.Exit==null||Known(choices.SingleOrDefault(c=>c.Key==options.Exit)));
        }
        private TransitionClipChoice PickFittingLibraryReference(IWin32Window owner,MDL0Node target,List<TransitionClipChoice> choices,List<LibrarySourceLease> leases)
        {
            string rig=BrawlRetargeter.SkeletonFingerprint(target);
            var catalogues=new[]{_library,_sharedSourceLibrary,_sessionSourceLibrary}.Concat(_sessionSourceCatalogues).Where(l=>l!=null).Distinct().ToArray();
            if(catalogues.Length==0)catalogues=new[]{new AnimationLibrary()};
            var rows=LibraryWork.Run(owner,"Finding this model’s library animations",(cancel,progress)=>{
                var result=new List<FittingLibraryItem>();
                foreach(var library in catalogues)
                {
                    cancel.ThrowIfCancellationRequested();
                    result.AddRange(library.Browse(new LibraryQuery{Channel="CHR0 skeletal",Availability=LibrarySourceState.Available})
                        .Where(i=>i.Model?.SkeletonSha256==rig).Select(i=>new FittingLibraryItem{Library=library,Item=i}));
                }
                return result.ToArray();
            });
            if(rows==null)return null;
            using(var dialog=new FittingReferencePicker(rows))
            {
                if(dialog.ShowDialog(owner)!=DialogResult.OK)return null;
                var picked=dialog.Selection;string key="library:"+picked.Item.Clip.Id+":"+picked.Item.Model.Id;
                var existing=choices.SingleOrDefault(c=>c.Key==key);if(existing!=null)return existing;
                var lease=LibraryWork.Run(owner,"Loading exact native reference",(cancel,progress)=>picked.Library.Open(picked.Item.Clip.Id,picked.Item.Model.Id,cancel));
                if(lease==null)return null;
                try
                {
                    if(lease.Reference.SourceRig!=rig||BrawlRetargeter.SkeletonFingerprint(lease.Model)!=rig||!ReferenceEquals(target,modelEditControl1.TargetModel))
                        throw new InvalidOperationException("This reference uses a different rig. Retarget it to the current model before fitting.");
                    string content=ExportContent(lease.Animation);var reference=lease.Reference;
                    var choice=new TransitionClipChoice{External=true,Clip=lease.Animation,Key=key,
                        Asset=new FighterAsset{Id=reference.ClipId,RigIdentity=rig,SourcePath=lease.MotionLock.Name},
                        Label="Library · "+reference.Game+" · "+picked.Item.Clip.SourceCharacter+" · "+Path.GetFileName(picked.Item.Motion.VirtualPath??picked.Item.Motion.LocalPath)+" ["+reference.ClipId.Substring(0,8)+"]",
                        Validate=()=>{if(lease.MotionRoot==null||ExportContent(lease.Animation)!=content)throw new InvalidOperationException("The frozen library reference changed. Choose it again.");}};
                    choices.Add(choice);leases.Add(lease);return choice;
                }
                catch{lease.Dispose();throw;}
            }
        }
    }
}
