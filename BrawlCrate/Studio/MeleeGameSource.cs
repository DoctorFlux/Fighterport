using FighterPort.Adapters.Melee;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
namespace BrawlCrate.Studio
{
    internal static class MeleeGameSource
    {
        internal const string NativeProvider="melee-hsd-file-v1";
        internal const string Channel="HSD skeletal";
        internal static bool IsDisc(string provider)=>provider==MeleeSource.DiscProvider;
        internal static bool IsSource(string provider)=>IsDisc(provider)||provider==MeleeSource.FolderProvider;
        internal static string BasePath(LibraryCatalogue catalogue,LibraryGameLocation source)
        {
            if(source.BaseSourceId==null)return null;
            var basis=catalogue.GameLocations.SingleOrDefault(s=>s.Id==source.BaseSourceId&&s.Indexed&&s.Game=="Melee");
            if(basis==null)throw new InvalidDataException("This mod folder's explicit Melee base source is unavailable. Restore or select that base source.");
            if(basis.BaseSourceId!=null)throw new NotSupportedException("Nested Melee overlays require their own dependency qualification. Choose a complete base source.");
            return basis.Path;
        }
        internal static void Validate(LibraryGameRequest request,LibraryCatalogue catalogue,CancellationToken cancellation)
        {
            var source=new LibraryGameLocation{BaseSourceId=request.BaseSourceId};
            using(var reader=new MeleeSource(request.Path,BasePath(catalogue,source),cancellation)){}
        }
        internal static LibraryGamePlan Prepare(LibraryGameRequest request,LibraryCatalogue catalogue,CancellationToken cancellation,Action<int,string> progress)
        {
            var location=new LibraryGameLocation{Id=Guid.NewGuid().ToString("N"),Game="Melee",GameVersion=request.GameVersion,Path=AnimationLibrary.OrdinaryPath(request.Path),Indexed=true,IsDirectory=Directory.Exists(request.Path),BaseSourceId=request.BaseSourceId};
            var plan=new LibraryGamePlan{Source=location};
            using(var reader=new MeleeSource(location.Path,BasePath(catalogue,location),cancellation))
            {
                progress?.Invoke(0,"Reading Melee native members without extracting the disc...");
                var inventory=MeleeInventory.Read(reader,cancellation,progress);plan.Issues.AddRange(inventory.Issues);
                location.Provider=reader.Provider;location.NativeGameId=reader.GameId??"Extracted HSD resources";location.NativeRevision=reader.Revision;location.NativeRegion=reader.GameId==null?"Build supplied by user":reader.GameId.Substring(3,1);
                location.InventorySha256=inventory.Sha256;location.NativeFiles=inventory.Files.Count;location.Label=location.IsDirectory?new DirectoryInfo(location.Path).Name:Path.GetFileName(location.Path);
                progress?.Invoke(91,"Hashing the immutable Melee source...");location.ContainerSha256=reader.SourceHash();location.Bytes=location.IsDirectory?inventory.Files.Sum(f=>(long)f.Bytes):new FileInfo(location.Path).Length;
                var origins=new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase);var mounts=new Dictionary<string,LibraryMount>(StringComparer.Ordinal);
                var sourceFiles=reader.Files.ToDictionary(f=>f.Path,StringComparer.Ordinal);
                foreach(var file in inventory.Files)
                {
                    cancellation.ThrowIfCancellationRequested();var original=sourceFiles[file.Path];bool disc=original.Offset>0;
                    var mount=new LibraryMount{Id=Guid.NewGuid().ToString("N"),Provider=NativeProvider,Game="Melee",GameVersion=request.GameVersion,Platform="GameCube",BuildEvidence=location.NativeGameId+" revision "+location.NativeRevision+"; exact HSD native resource identities; mod/base association explicit",LocalPath=original.PhysicalPath,ContainerSha256=file.Sha256,Bytes=file.Bytes,NativeWriteTicks=File.GetLastWriteTimeUtc(original.PhysicalPath).Ticks,VirtualPath=file.Path,GameSourceId=location.Id};
                    if(disc)
                    {
                        if(!origins.TryGetValue(original.PhysicalPath,out string hash))
                        {if(!reader.IsDirectory&&string.Equals(original.PhysicalPath,reader.SourcePath,StringComparison.OrdinalIgnoreCase))hash=location.ContainerSha256;else using(var origin=new MeleeSource(original.PhysicalPath,null,cancellation))hash=origin.SourceHash();origins.Add(original.PhysicalPath,hash);}
                        var info=new FileInfo(original.PhysicalPath);mount.OriginPath=original.PhysicalPath;mount.OriginSha256=hash;mount.OriginBytes=info.Length;mount.OriginWriteTicks=info.LastWriteTimeUtc.Ticks;mount.OriginPartition="GAMECUBE";
                    }
                    mounts.Add(file.Path,mount);plan.Mounts.Add(mount);
                }
                var models=new Dictionary<string,LibraryModel>();
                foreach(var model in inventory.Models)
                {
                    var record=new LibraryModel{Id=Guid.NewGuid().ToString("N"),MountId=mounts[model.File].Id,Character=Friendly(model.Actor),Variant=Path.GetFileNameWithoutExtension(model.File),Name=model.Symbol,ResourceKey=model.Key,ResourcePath=model.Symbol,ResourceSha256=model.Sha256,SkeletonSha256=model.RigHash,RestInterpretation="melee-hsd-authentic-bind-v1",NativeVersion=1,HasGeometry=true};
                    plan.Models.Add(record);models.Add(model.File+"|"+model.Key,record);
                }
                foreach(var motion in inventory.Motions)
                {
                    LibraryModel model=null;if(motion.ModelKey!=null)models.TryGetValue(motion.ModelKey,out model);
                    var record=new LibraryAnimation{Id=Guid.NewGuid().ToString("N"),MountId=mounts[motion.File].Id,ModelId=model?.Id,Name=MotionName(motion.Symbol),SourceCharacter=Friendly(motion.Actor),ResourceKey=motion.Key,ResourcePath=motion.Symbol,ResourceSha256=motion.Sha256,Channel=Channel,NativeVersion=1,FrameCount=checked((int)Math.Ceiling(motion.LastFrame)+1),Loop=false,TicksPerSecond=60,TimebaseEvidence="Melee native frames; stored HSD end frame "+motion.LastFrame.ToString("R",System.Globalization.CultureInfo.InvariantCulture)+"; Figatree runtime loop policy is not encoded here",Interpolation="Native "+motion.Kind+" FOBJ opcodes, values and slopes; decoded once before playback",AssociationEvidence=model==null?null:"Exact native exported model/action symbol association; native joint order; source-instance scoped",AssociationIssue=model==null?"Missing or ambiguous exact native model symbol association.":null};
                    plan.Clips.Add(record);if(model!=null&&!model.MotionMountIds.Contains(record.MountId))model.MotionMountIds.Add(record.MountId);
                }
                // Alternate costume bindings require the complete exact rig fingerprint,
                // not a costume filename, abbreviated actor name or skin-weight subset.
                var anchors=plan.Models.Where(m=>m.MotionMountIds.Count>0).ToArray();
                foreach(var model in plan.Models)
                {
                    var candidates=anchors.Where(m=>m.SkeletonSha256==model.SkeletonSha256).ToArray();
                    if(candidates.Length==1)
                    {model.Character=candidates[0].Character;foreach(string mount in candidates[0].MotionMountIds)if(!model.MotionMountIds.Contains(mount))model.MotionMountIds.Add(mount);}
                }
                progress?.Invoke(100,"Prepared Melee models, costumes and native animations.");return plan;
            }
        }
        private static string Friendly(string actor)=>actor!=null&&actor.StartsWith("Ply",StringComparison.Ordinal)&&actor.EndsWith("5K",StringComparison.Ordinal)?actor.Substring(3,actor.Length-5):actor??"Unassociated";
        private static string MotionName(string symbol)
        {int at=symbol.IndexOf("_ACTION_",StringComparison.Ordinal);string value=at<0?symbol:symbol.Substring(at+8);foreach(string end in new[]{"_figatree","_anim_joint"})if(value.EndsWith(end,StringComparison.Ordinal))value=value.Substring(0,value.Length-end.Length);return value;}
        internal static void VerifyPlan(LibraryGamePlan plan,CancellationToken cancellation)
        {
            // Locks are taken again by Open; registration also rejects any source change.
            foreach(var group in plan.Mounts.GroupBy(m=>m.OriginPath??m.LocalPath,StringComparer.OrdinalIgnoreCase))
            {
                var first=group.First();
                if(first.OriginPath!=null)
                {
                    using(var reader=new MeleeSource(first.OriginPath,null,cancellation))
                    {
                        if(reader.SourceHash()!=first.OriginSha256)throw new InvalidDataException("Melee source image changed during indexing.");
                        foreach(var mount in group)if(reader.FileHash(mount.VirtualPath)!=mount.ContainerSha256)throw new InvalidDataException("Melee native member changed during indexing.");
                    }
                }
                else foreach(var mount in group)
                {cancellation.ThrowIfCancellationRequested();using(var held=AnimationLibrary.LockSource(mount.LocalPath))if(held.Length!=mount.Bytes||FighterProject.HashFile(mount.LocalPath)!=mount.ContainerSha256)throw new InvalidDataException("Melee folder member changed during indexing.");}
            }
        }
        internal static byte[] Read(LibraryMount mount,CancellationToken cancellation)
        {
            byte[] bytes;
            if(mount.OriginPath!=null){using(var reader=new MeleeSource(mount.OriginPath,null,cancellation))bytes=reader.Read(mount.VirtualPath);}
            else{AnimationLibrary.NoLinks(mount.LocalPath);using(var held=AnimationLibrary.LockSource(mount.LocalPath)){if(held.Length!=mount.Bytes)throw new InvalidDataException("Melee source length changed.");bytes=new byte[checked((int)held.Length)];int done=0;while(done<bytes.Length){cancellation.ThrowIfCancellationRequested();int read=held.Read(bytes,done,Math.Min(65536,bytes.Length-done));if(read==0)throw new EndOfStreamException();done+=read;}}}
            if(bytes.Length!=mount.Bytes||MeleeSource.Sha256(bytes)!=mount.ContainerSha256)throw new InvalidDataException("The immutable Melee resource changed. Reindex this source before using it.");return bytes;
        }
        internal static MeleePartVisibility PrepareParts(LibraryCatalogue data,LibraryAnimation clip,LibraryModel selectedModel,MeleeNativeModel model,int count,List<FileStream> locks,CancellationToken cancellation,out string notice)
        {
            notice="Part context unavailable: all native parts are shown.";
            var motionMount=data.Mounts.Single(m=>m.Id==clip.MountId);
            string virtualPath=motionMount.VirtualPath;
            if(virtualPath==null||!virtualPath.EndsWith("AJ.dat",StringComparison.OrdinalIgnoreCase))return null;
            string contextPath=virtualPath.Substring(0,virtualPath.Length-6)+".dat";
            var context=data.Mounts.SingleOrDefault(m=>m.GameSourceId==motionMount.GameSourceId&&string.Equals(m.VirtualPath,contextPath,StringComparison.OrdinalIgnoreCase));
            string actor=MeleeInventory.MotionActor(clip.ResourcePath??"");
            if(context==null||actor==null||!actor.StartsWith("Ply",StringComparison.Ordinal)||!actor.EndsWith("5K",StringComparison.Ordinal))return null;
            string symbol="ftData"+actor.Substring(3,actor.Length-5);
            try
            {
                string path=context.OriginPath??context.LocalPath;
                if(!locks.Any(s=>string.Equals(s.Name,path,StringComparison.OrdinalIgnoreCase))){AnimationLibrary.NoLinks(path);locks.Add(new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.Read));}
                var members=MeleeNative.ReadMembers(Read(context,cancellation),cancellation);
                var candidates=members.SelectMany(m=>m.Native.Roots.Where(r=>r.Name==symbol&&r.Data is HSDRaw.Melee.Pl.SBM_FighterData).Select(r=>new{Member=m,Root=r})).ToArray();
                if(candidates.Length!=1)throw new InvalidDataException("No unique exact fighter-data symbol association.");
                var matched=candidates[0];var tables=((HSDRaw.Melee.Pl.SBM_FighterData)matched.Root.Data).ModelLookupTables;
                var modelMount=data.Mounts.Single(m=>m.Id==selectedModel.MountId);var location=data.GameLocations.Single(l=>l.Id==motionMount.GameSourceId);
                int? costumeIndex=null;string bindingHash="common-tables";
                try
                {
                    using(var reader=new MeleeSource(location.Path,BasePath(data,location),cancellation))
                    {var dol=reader.ReadExecutable();costumeIndex=MeleeCostumeBinding.FindIndex(dol,Path.GetFileName(modelMount.VirtualPath),model.Symbol,actor,tables.CostumeVisibilityLookups.Length,cancellation);bindingHash=MeleeSource.Sha256(dol);}
                }
                catch(Exception e)when(e is IOException||e is InvalidDataException||e is NotSupportedException){/* Only identical effective tables may proceed without an exact executable binding. */}
                var parts=MeleePartVisibility.Create(tables,count,matched.Member.Sha256,matched.Member.Offset+"/"+symbol+"; "+bindingHash+"; costume "+costumeIndex,costumeIndex);
                notice="Parts use native high-detail variant 1 for inspection; choose Parts to review. Game-script defaults are not inferred.";return parts;
            }
            catch(Exception e)when(e is IOException||e is InvalidDataException||e is ArgumentException||e is NotSupportedException||e is IndexOutOfRangeException)
            {notice="Part context unavailable: "+e.Message+" All native parts are shown.";return null;}
        }
        internal static void ExportOriginal(LibraryCatalogue data,LibraryAnimation clip,LibraryModel model,string destination,CancellationToken cancellation)
        {
            if(!Costumes(data,clip).Any(c=>c.ModelId==model.Id&&c.Available))throw new InvalidDataException("Choose an exact compatible costume from this same native source instance.");
            var files=new Dictionary<string,byte[]>(StringComparer.OrdinalIgnoreCase);
            var mounts=new[]{data.Mounts.Single(m=>m.Id==model.MountId),data.Mounts.Single(m=>m.Id==clip.MountId)}.GroupBy(m=>m.Id).Select(g=>g.First()).ToArray();
            var locks=new List<FileStream>();
            try
            {
                foreach(string source in mounts.Select(m=>m.OriginPath??m.LocalPath).Distinct(StringComparer.OrdinalIgnoreCase)){AnimationLibrary.NoLinks(source);locks.Add(new FileStream(source,FileMode.Open,FileAccess.Read,FileShare.Read));}
                foreach(var mount in mounts){cancellation.ThrowIfCancellationRequested();files.Add(mount.VirtualPath,Read(mount,cancellation));}
                FighterPort.Adapters.Melee.MeleeSourceFileExport.Publish(files,destination,data.GameLocations.Select(l=>l.Path),cancellation);
            }
            finally{foreach(var stream in locks)stream.Dispose();}
        }
        internal static LibraryCostumeOption[] Costumes(LibraryCatalogue data,LibraryAnimation clip)
        {
            if(clip.ModelId==null)return new LibraryCostumeOption[0];var canonical=data.Models.Single(m=>m.Id==clip.ModelId);var origin=data.Mounts.Single(m=>m.Id==canonical.MountId);
            return data.Models.Where(m=>m.SkeletonSha256==canonical.SkeletonSha256&&m.MotionMountIds.Contains(clip.MountId)&&data.Mounts.Single(a=>a.Id==m.MountId).GameSourceId==origin.GameSourceId).Select(m=>new LibraryCostumeOption{ModelId=m.Id,Name=m.Name,Variant=m.Variant,Label=m.Variant,IsCanonical=m.Id==canonical.Id,Available=true}).OrderByDescending(m=>m.IsCanonical).ThenBy(m=>m.Label,StringComparer.Ordinal).ToArray();
        }
    }
}
