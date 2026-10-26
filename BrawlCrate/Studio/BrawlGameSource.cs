using BrawlCrate.Studio.Retargeting;
using BrawlLib.SSBB.ResourceNodes;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;

namespace BrawlCrate.Studio
{
    public sealed class LibraryGameRequest
    {
        public string Game {get;set;}="Brawl";
        public string GameVersion {get;set;}="Build not supplied";
        public string Path {get;set;}
        // Legacy request compatibility only. The built-in reader never executes it.
        public string DolphinToolPath {get;set;}
        public bool ApproveBuild {get;set;}
        public string[] Characters {get;set;}
    }
    public sealed class LibraryGamePlan
    {
        public LibraryGameLocation Source {get;internal set;}
        public List<LibraryMount> Mounts {get;}=new List<LibraryMount>();
        public List<LibraryModel> Models {get;}=new List<LibraryModel>();
        public List<LibraryAnimation> Clips {get;}=new List<LibraryAnimation>();
        public List<string> Issues {get;}=new List<string>();
    }
    // Native Brawl catalogue with an application-owned read-only ISO/WBFS reader.
    public static class BrawlGameSource
    {
        private sealed class ModelFacts
        {
            internal LibraryModel Model;
            internal HashSet<string> Bones,Body;
            internal string Path;
        }
        private sealed class ClipFacts
        {
            internal LibraryAnimation Clip;
            internal string[] Bones;
            internal string Character, Path;
        }
        private static string ResourceScope(string key)
        {int last=key.LastIndexOf('/');return last<0?key:key.Substring(0,last).Substring(0,Math.Max(0,key.Substring(0,last).LastIndexOf('/')));}
        private static ModelFacts UniqueModel(ModelFacts[] candidates) =>
            candidates.Length>0&&candidates.Select(c=>c.Model.ResourceSha256).Distinct(StringComparer.Ordinal).Count()==1?candidates[0]:null;
        private static string Digest(string text)
        {using(var sha=SHA256.Create())return BrawlLibrarySource.Hex(sha.ComputeHash(Encoding.UTF8.GetBytes(text)));}
        internal static string HashDisc(string path,CancellationToken cancellation)
        {
            EnsureSingleDiscSource(path);
            using(var input=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.Read))using(var sha=SHA256.Create())
            {
                var buffer=new byte[1024*1024];int count;
                while((count=input.Read(buffer,0,buffer.Length))>0){cancellation.ThrowIfCancellationRequested();sha.TransformBlock(buffer,0,count,buffer,0);}
                sha.TransformFinalBlock(buffer,0,0);return BrawlLibrarySource.Hex(sha.Hash);
            }
        }
        // A complete source identity covers the selected file. Split WBFS
        // companions require their own complete manifest/hash support first.
        internal static void EnsureSingleDiscSource(string path)
        {
            if(!string.Equals(System.IO.Path.GetExtension(path),".wbfs",StringComparison.OrdinalIgnoreCase))return;
            string directory=System.IO.Path.GetDirectoryName(path),stem=System.IO.Path.GetFileNameWithoutExtension(path);
            if(string.IsNullOrEmpty(directory)||!Directory.Exists(directory))return;
            string pattern="^"+Regex.Escape(stem)+@"\.wbf[0-9]+$";int seen=0;
            foreach(string companion in Directory.EnumerateFiles(directory,stem+".wbf*",SearchOption.TopDirectoryOnly))
            {
                if(++seen>256||Regex.IsMatch(System.IO.Path.GetFileName(companion),pattern,RegexOptions.IgnoreCase))
                    throw new InvalidDataException("Split WBFS files need a reader that verifies every part. Choose a single-file WBFS or ISO for this workflow.");
            }
        }

        internal static bool IsDiscProvider(string provider) => provider == BrawlDiscImage.Provider || provider == "dolphin-brawl-disc-v1";
        internal static void ValidateDiscSource(LibraryGameRequest request,CancellationToken cancellation=default)
        {
            if(request==null)throw new ArgumentNullException(nameof(request));
            request.Path=AnimationLibrary.OrdinaryPath(request.Path);
            using(var image=new BrawlDiscImage(request.Path,cancellation)) { }
        }

        internal static void ValidateGameSource(LibraryGameRequest request,CancellationToken cancellation=default)
        {
            if(request==null||!new[]{"Brawl","Project M","Project+"}.Contains(request.Game))
                throw new NotSupportedException("Choose a Brawl-family source. Other games require their own readers.");
            string path=AnimationLibrary.OrdinaryPath(request.Path);AnimationLibrary.NoLinks(path);
            if(!Directory.Exists(path)){ValidateDiscSource(request,cancellation);return;}
            request.Path=path;string root=FighterFolder(path);int characters=0;
            foreach(string folder in Directory.EnumerateDirectories(root))
            {
                cancellation.ThrowIfCancellationRequested();AnimationLibrary.NoLinks(folder);
                if(++characters>10000)throw new InvalidDataException("The Brawl character-folder inventory exceeds its bound.");
            }
            if(characters==0)throw new InvalidDataException("Choose an extracted Brawl build containing fighter/character folders.");
            cancellation.ThrowIfCancellationRequested();
        }

        private static string[] DiscFiles(LibraryGameRequest request,CancellationToken cancellation)
        { using(var image=new BrawlDiscImage(request.Path,cancellation))return image.Files; }
        internal static string FighterFolder(string source)
        {
            var candidates=new[]{source,System.IO.Path.Combine(source,"fighter"),System.IO.Path.Combine(source,"files","fighter"),System.IO.Path.Combine(source,"DATA","files","fighter"),
                System.IO.Path.Combine(source,"pf","fighter"),System.IO.Path.Combine(source,"projectm","pf","fighter"),System.IO.Path.Combine(source,"project+","pf","fighter"),System.IO.Path.Combine(source,"private","wii","app","rsbe","pf","fighter")}
                .Where(p=>Directory.Exists(p)&&string.Equals(new DirectoryInfo(p).Name,"fighter",StringComparison.OrdinalIgnoreCase)).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
            if(candidates.Length!=1)throw new InvalidDataException(candidates.Length==0?"Choose an extracted game or SD build containing its fighter folder.":"This path contains multiple builds. Choose the specific build's pf/fighter folder.");
            AnimationLibrary.NoLinks(candidates[0]);return candidates[0];
        }
        public static string[] BrowseCharacters(LibraryGameRequest request,CancellationToken cancellation=default)
        {
            if(request==null||!new[]{"Brawl","Project M","Project+"}.Contains(request.Game))throw new NotSupportedException("Native game browsing currently supports Brawl-family files. Other game paths can be saved for later adapters.");
            string path=AnimationLibrary.OrdinaryPath(request.Path);AnimationLibrary.NoLinks(path);
            if(File.Exists(path))
            {
                if(!new[]{".iso",".wbfs"}.Contains(System.IO.Path.GetExtension(path).ToLowerInvariant()))throw new NotSupportedException("Choose a Brawl ISO or single-file WBFS.");
                ValidateDiscSource(request,cancellation);
                using(var held=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.Read))
                    return DiscFiles(request,cancellation).Where(p=>p.Split('/').Length>=3).Select(p=>p.Split('/')[1]).Distinct(StringComparer.Ordinal).OrderBy(p=>p,StringComparer.Ordinal).ToArray();
            }
            return Directory.EnumerateDirectories(FighterFolder(path)).Select(p=>{AnimationLibrary.NoLinks(p);return System.IO.Path.GetFileName(p);}).OrderBy(p=>p,StringComparer.Ordinal).ToArray();
        }
        private static IEnumerable<string> NativeFiles(string root,CancellationToken cancellation)
        {
            var pending=new Stack<string>();pending.Push(root);int directories=0,files=0;
            while(pending.Count>0)
            {
                cancellation.ThrowIfCancellationRequested();string directory=pending.Pop();AnimationLibrary.NoLinks(directory);
                if(++directories>10000)throw new InvalidDataException("Too many source folders.");
                foreach(string child in Directory.EnumerateDirectories(directory))pending.Push(child);
                foreach(string path in Directory.EnumerateFiles(directory))
                    if(new[]{".pac",".pcs",".brres",".mdl0",".chr0"}.Contains(System.IO.Path.GetExtension(path).ToLowerInvariant()))
                    {if(++files>10000)throw new InvalidDataException("Too many native source files.");AnimationLibrary.NoLinks(path);yield return path;}
            }
        }
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
        private static void ReadNativePackage(string path,LibraryMount mount,string relative,string character,LibraryGamePlan plan,
            List<ModelFacts> models,List<ClipFacts> clips,CancellationToken cancellation)
        {
            using(var held=AnimationLibrary.LockSource(path))
            using(var root=NodeFactory.FromFile(null,path))
            {
                if(root==null){plan.Issues.Add(relative+": unsupported native package");return;}
                foreach(var resource in BrawlLibrarySource.Resources(root,cancellation))
                {
                    if(resource.Item1 is MDL0Node model)
                    {
                        var metadata=BrawlLibrarySource.DescribeModel(model,resource.Item2);metadata.MountId=mount.Id;metadata.Character=character;metadata.Variant=System.IO.Path.GetFileName(path);
                        var bones=model.AllBones.ToArray();var semantic=BrawlRetargeter.Characterize(model);
                        models.Add(new ModelFacts{Model=metadata,Path=relative,Bones=new HashSet<string>(bones.Select(b=>b.Name)),
                            Body=new HashSet<string>(semantic.Roles.Where(p=>p.Key=="pelvis"||p.Key=="spine"||p.Key=="chest"||p.Key=="neck"||p.Key.EndsWith(".upperLeg")||p.Key.EndsWith(".upperArm")).Select(p=>bones[p.Value].Name))});
                        plan.Models.Add(metadata);
                    }
                    else if(resource.Item1 is NW4RAnimationNode motion)
                    {
                        var metadata=new LibraryAnimation{Id=Guid.NewGuid().ToString("N"),MountId=mount.Id,Name=motion.Name,SourceCharacter=character,ResourceKey=resource.Item2,ResourcePath=motion.TreePath,
                            ResourceSha256=BrawlLibrarySource.ResourceHash(motion),Channel=motion is CHR0Node?"CHR0 skeletal":motion.GetType().Name,NativeVersion=motion.Version,FrameCount=motion.FrameCount,Loop=motion.Loop,
                            Tags=character+" "+mount.Game,AssociationIssue="Original source model requires verified association"};
                        clips.Add(new ClipFacts{Clip=metadata,Character=character,Path=relative,Bones=motion is CHR0Node?motion.Children.Select(n=>n.Name).ToArray():new string[0]});plan.Clips.Add(metadata);
                    }
                }
            }
        }
        public static LibraryGamePlan Prepare(LibraryGameRequest request,string cacheRoot,CancellationToken cancellation=default,Action<int,string> progress=null)
        {
            cancellation.ThrowIfCancellationRequested();
            if(request==null||!new[]{"Brawl","Project M","Project+"}.Contains(request.Game))throw new NotSupportedException("Whole-build native indexing currently supports Brawl-family sources. Other game paths can be saved; their native adapters are pending.");
            if(!request.ApproveBuild||string.IsNullOrWhiteSpace(request.GameVersion))throw new InvalidDataException("Confirm the selected original game/build before indexing its complete roster.");
            bool all=request.Characters==null||request.Characters.Length==0;
            string[] characters=all?null:request.Characters.Distinct(StringComparer.Ordinal).ToArray();
            if(!all&&(characters.Length>256||characters.Any(c=>!Regex.IsMatch(c??"",@"^[A-Za-z0-9_-]+$"))))throw new InvalidDataException("Invalid native character scope.");
            string sourcePath=AnimationLibrary.OrdinaryPath(request.Path);AnimationLibrary.NoLinks(sourcePath);
            var plan=new LibraryGamePlan{Source=new LibraryGameLocation{Id=Guid.NewGuid().ToString("N"),Game=request.Game,GameVersion=request.GameVersion,Path=sourcePath,Indexed=true,IsDirectory=Directory.Exists(sourcePath)}};
            string fighterRoot;string originHash=null;long originBytes=0,originTicks=0;
            FileStream sourceLock=null;BrawlDiscImage discReader=null;
            try
            {
                if(File.Exists(sourcePath))
                {
                    discReader=new BrawlDiscImage(sourcePath,cancellation);
                    sourceLock=new FileStream(sourcePath,FileMode.Open,FileAccess.Read,FileShare.Read);
                    progress?.Invoke(1,"Hashing original disc image (read-only)...");originHash=HashDisc(sourcePath,cancellation);originBytes=sourceLock.Length;originTicks=File.GetLastWriteTimeUtc(sourcePath).Ticks;
                    plan.Source.NativeGameId=discReader.GameId;plan.Source.NativeRevision=discReader.Revision;plan.Source.NativeRegion=discReader.Region;
                    plan.Source.GameVersion=request.GameVersion+" · "+plan.Source.NativeGameId+" revision "+plan.Source.NativeRevision;
                    progress?.Invoke(3,"Listing fighters with the built-in Brawl disc reader...");
                    var allPaths=discReader.Files;
                    if(all)characters=allPaths.Where(p=>p.Split('/').Length>=3).Select(p=>p.Split('/')[1]).Distinct(StringComparer.Ordinal).OrderBy(c=>c,StringComparer.Ordinal).ToArray();
                    if(characters.Any(c=>!allPaths.Any(p=>p.Split('/').Length>=3&&p.Split('/')[1]==c)))throw new InvalidDataException("A selected character is not present in this image.");
                    var selected=new HashSet<string>(characters,StringComparer.Ordinal);
                    var paths=all?allPaths:allPaths.Where(p=>p.Split('/').Length>=3&&selected.Contains(p.Split('/')[1])).ToArray();
                    if(discReader.Inventory.Where(e=>all||e.Path.Split('/').Length>=3&&selected.Contains(e.Path.Split('/')[1])).Sum(e=>e.Length)>4L*1024*1024*1024)
                        throw new InvalidDataException("The selected fighter subtree exceeds its 4 GB cache bound.");
                    plan.Source.InventorySha256=Digest(string.Join("\n",paths));plan.Source.Provider=BrawlDiscImage.Provider;
                    string cache=System.IO.Path.Combine(AnimationLibrary.OrdinaryPath(cacheRoot),Guid.NewGuid().ToString("N"));AnimationLibrary.NoLinks(cache);
                    fighterRoot=System.IO.Path.Combine(cache,"DATA","files","fighter");
                    if(all)
                    {
                        progress?.Invoke(5,"Reading the complete Brawl fighter library (original disc is read-only)...");
                        discReader.ExtractAll(System.IO.Path.Combine(cache,"DATA","files"));
                    }
                    else foreach(string character in characters)
                    {
                        progress?.Invoke(5,"Reading selected character: "+character+" (built-in reader; original image is read-only)...");
                        discReader.Extract(character,System.IO.Path.Combine(cache,"DATA","files"));
                    }
                    if(!paths.All(p=>File.Exists(System.IO.Path.Combine(cache,"DATA","files",p.Replace('/',System.IO.Path.DirectorySeparatorChar)))))throw new InvalidDataException("Selective disc reading did not produce every listed fighter file.");
                    plan.Source.ContainerSha256=originHash;plan.Source.Bytes=originBytes;plan.Source.Label=System.IO.Path.GetFileNameWithoutExtension(sourcePath);
                }
                else
                {
                    if(!Directory.Exists(sourcePath))throw new DirectoryNotFoundException("Choose an existing extracted game/build folder.");
                    fighterRoot=FighterFolder(sourcePath);
                    if(all)characters=Directory.EnumerateDirectories(fighterRoot).Select(System.IO.Path.GetFileName).ToArray();
                    plan.Source.Provider="brawl-folder-v1";plan.Source.Label=new DirectoryInfo(sourcePath).Name;
                }
                var files=(all?NativeFiles(fighterRoot,cancellation):characters.SelectMany(c=>NativeFiles(System.IO.Path.Combine(fighterRoot,c),cancellation))).OrderBy(p=>p,StringComparer.OrdinalIgnoreCase).Take(10001).ToArray();
                if(files.Length==0||files.Length>10000)throw new InvalidDataException("Native source file inventory exceeds its bounded limit or is empty.");
                long totalBytes=0;var models=new List<ModelFacts>();var clips=new List<ClipFacts>();
                foreach(string path in files)
                {
                    cancellation.ThrowIfCancellationRequested();AnimationLibrary.NoLinks(path);var info=new FileInfo(path);totalBytes+=info.Length;
                    if(totalBytes>4L*1024*1024*1024)throw new InvalidDataException("The selected native fighter subtree exceeds the 4 GB cache/indexing bound.");
                    string relative="fighter/"+path.Substring(fighterRoot.Length+1).Replace('\\','/');string character=relative.Split('/').Length>=3?relative.Split('/')[1]:"Shared fighter resources";
                    var mount=new LibraryMount{Id=Guid.NewGuid().ToString("N"),Game=request.Game,GameVersion=plan.Source.GameVersion,LocalPath=path,ContainerSha256=FighterProject.HashFile(path),Bytes=info.Length,
                        BuildEvidence="User-confirmed game/build; "+plan.Source.Provider+"; native resource indices, source hashes and body-track binding checked",GameSourceId=plan.Source.Id,VirtualPath=relative,
                        OriginPath=originHash==null?null:sourcePath,OriginSha256=originHash,OriginBytes=originBytes,OriginWriteTicks=originTicks,OriginPartition=originHash==null?null:"DATA"};plan.Mounts.Add(mount);
                    progress?.Invoke(10+plan.Mounts.Count*75/files.Length,"Indexing "+plan.Mounts.Count+" / "+files.Length+" native fighter packages: "+relative);
                    ReadNativePackage(path,mount,relative,character,plan,models,clips,cancellation);
                    // Native key/geometry graphs can fragment the 32-bit heap.
                    // Collect only between disposed packages during explicit
                    // preparation, never from preview or playback paths.
                    if(plan.Mounts.Count%8==0)
                    {
                        System.Runtime.GCSettings.LargeObjectHeapCompactionMode=System.Runtime.GCLargeObjectHeapCompactionMode.CompactOnce;
                        GC.Collect(2,GCCollectionMode.Forced,true,true);
                        GC.WaitForPendingFinalizers();
                    }
                }
                // Native resource ownership selects candidates; exact authored
                // track binding remains required. No retarget-role inference is
                // used for local article/effect animation associations.
                foreach(var clip in clips)
                {
                    cancellation.ThrowIfCancellationRequested();
                    if(clip.Clip.Channel!="CHR0 skeletal"||(clip.Clip.NativeVersion!=4&&clip.Clip.NativeVersion!=5)||clip.Clip.FrameCount>4096){clip.Clip.AssociationIssue="Unsupported channel/version";continue;}
                    string scope=ResourceScope(clip.Clip.ResourceKey);
                    var local=models.Where(m=>m.Model.MountId==clip.Clip.MountId&&ResourceScope(m.Model.ResourceKey)==scope&&
                        m.Model.HasGeometry&&!m.Model.Shadow&&clip.Bones.Length>0&&clip.Bones.All(m.Bones.Contains)).ToArray();
                    ModelFacts chosen=UniqueModel(local);
                    string evidence="same native resource package and resource scope; every authored track binds to a unique original model";
                    if(chosen==null)
                    {
                        // Brawl's native MotionEtc family identifies its costume
                        // container. This excludes Spy/copy families merely sharing
                        // a folder, and disambiguates multi-model body containers.
                        var family=Regex.Match(System.IO.Path.GetFileName(clip.Path),@"^(Fit.+?)MotionEtc(?:_[A-Za-z]{2})?\.(pac|pcs)$",RegexOptions.IgnoreCase|RegexOptions.CultureInvariant);
                        var candidates=models.Where(m=>m.Model.Character==clip.Character&&m.Model.HasGeometry&&!m.Model.Shadow&&
                            (family.Success?string.Equals(System.IO.Path.GetFileNameWithoutExtension(m.Path),family.Groups[1].Value+"00",StringComparison.OrdinalIgnoreCase):
                                Regex.IsMatch(System.IO.Path.GetFileName(m.Path),@"^Fit.+00\.(pac|pcs)$",RegexOptions.IgnoreCase))&&
                            clip.Bones.Length>0&&clip.Bones.Count(b=>!m.Bones.Contains(b))<=clip.Bones.Length/10&&
                            (family.Success||clip.Bones.Count(m.Body.Contains)>=2)).ToArray();
                        if(family.Success)
                        {
                            var identified=candidates.Where(m=>string.Equals(m.Model.Name,family.Groups[1].Value+"00",StringComparison.OrdinalIgnoreCase)).ToArray();
                            if(identified.Length>0)candidates=identified;
                        }
                        chosen=UniqueModel(candidates);
                        evidence="native fighter-folder and MotionEtc/costume family ownership; unique original model identity; at least 90% of authored tracks bind; auxiliary losses explicitly reported";
                    }
                    if(chosen==null){clip.Clip.AssociationIssue="No unique native model binding; resources remain indexed and require association review";continue;}
                    clip.Clip.ModelId=chosen.Model.Id;clip.Clip.AssociationIssue=null;
                    var unbound=clip.Bones.Where(b=>!chosen.Bones.Contains(b)).ToArray();clip.Clip.UnboundSourceTracks=unbound.Length==0?null:string.Join(", ",unbound);
                    clip.Clip.AssociationEvidence="User-confirmed original game/build; exact container/resource and skeleton hashes; "+evidence;
                    if(!chosen.Model.MotionMountIds.Contains(clip.Clip.MountId))chosen.Model.MotionMountIds.Add(clip.Clip.MountId);
                }
                if(plan.Source.InventorySha256==null)plan.Source.InventorySha256=Digest(string.Join("\n",plan.Mounts.Select(m=>m.VirtualPath+":"+m.ContainerSha256)));
                plan.Source.NativeFiles=files.Length;if(plan.Source.ContainerSha256==null)plan.Source.ContainerSha256=plan.Source.InventorySha256;
                if(plan.Models.Count==0||plan.Clips.Count==0||plan.Clips.Count>AnimationLibrary.MaximumClips)throw new InvalidDataException("No supported model/motion inventory was found, or the source exceeds catalogue limits.");
                progress?.Invoke(100,"Complete roster indexed: "+plan.Models.Count+" models, "+plan.Clips.Count+" animation resources.");return plan;
            }
            finally{discReader?.Dispose();sourceLock?.Dispose();}
        }
    }
}
