using BrawlCrate.Studio.Retargeting;
using BrawlLib.SSBB.ResourceNodes;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Web.Script.Serialization;

namespace BrawlCrate.Studio
{
    public sealed class LibraryGameRequest
    {
        public string Game {get;set;}="Brawl";
        public string GameVersion {get;set;}="Build not supplied";
        public string Path {get;set;}
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
    // Native Brawl catalogue, not an importer for other titles. Dolphin remains
    // an external, explicitly selected, hash-pinned read-only CLI dependency.
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
            internal string Character;
        }
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

        internal static void ValidateDiscSource(LibraryGameRequest request,CancellationToken cancellation=default)
        {
            if(request==null)throw new ArgumentNullException(nameof(request));
            string image=AnimationLibrary.OrdinaryPath(request.Path),tool=AnimationLibrary.OrdinaryPath(request.DolphinToolPath);
            AnimationLibrary.NoLinks(image);AnimationLibrary.NoLinks(tool);EnsureSingleDiscSource(image);
            if(!File.Exists(image)||!new[]{".iso",".wbfs",".rvz"}.Contains(System.IO.Path.GetExtension(image).ToLowerInvariant()))
                throw new InvalidDataException("Choose an existing Brawl ISO or WBFS file. The current reader also accepts RVZ.");
            if(!File.Exists(tool)||!string.Equals(System.IO.Path.GetFileName(tool),"DolphinTool.exe",StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("Choose an installed DolphinTool.exe with header and extract commands.");
            request.Path=image;request.DolphinToolPath=tool;
            cancellation.ThrowIfCancellationRequested();
            string help=ProbeReader(tool,"--help",cancellation,false);
            if(!Regex.IsMatch(help,@"\bextract\b",RegexOptions.IgnoreCase)||!Regex.IsMatch(help,@"\bheader\b",RegexOptions.IgnoreCase))
                throw new InvalidDataException("This DolphinTool is too old for disc browsing. Select a build with header and extract commands; convert/verify-only builds cannot read the library.");
            string extract=ProbeReader(tool,"extract -h",cancellation,false),header=ProbeReader(tool,"header -h",cancellation,false);
            if(!extract.Contains("--list")||!extract.Contains("--single")||!header.Contains("--json"))
                throw new InvalidDataException("This reader lacks the required extract --list/--single and header --json options. Choose a compatible modern DolphinTool.");
            using(var held=new FileStream(image,FileMode.Open,FileAccess.Read,FileShare.Read))
            {
                if(held.Length<=0||held.Length>64L*1024*1024*1024)throw new InvalidDataException("Choose a nonempty Brawl image within the reader's 64 GB limit.");
                string json=ProbeReader(tool,"header -i "+Quote(image)+" -j",cancellation,true);
                var info=new JavaScriptSerializer{MaxJsonLength=1024*1024}.Deserialize<Dictionary<string,object>>(json);
                if(info==null||!info.TryGetValue("game_id",out var game)||!Regex.IsMatch(game?.ToString()??"",@"^RSB[A-Z0-9]{3}$")||
                    !info.TryGetValue("revision",out var revision)||!(revision is int nativeRevision)||nativeRevision<0||nativeRevision>255)
                    throw new InvalidDataException("This disc is not identified as Brawl. Other games require their own source readers.");
            }
            cancellation.ThrowIfCancellationRequested();
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

        private static string ProbeReader(string executable,string arguments,CancellationToken cancellation,bool json)
        {
            var output=new StringBuilder();var error=new StringBuilder();var sync=new object();bool overflow=false;
            using(var process=new Process{StartInfo=new ProcessStartInfo(executable,arguments){UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=true,RedirectStandardError=true,WorkingDirectory=System.IO.Path.GetDirectoryName(executable)}})
            {
                DataReceivedEventHandler read=(s,e)=>{if(e.Data==null)return;lock(sync){if(output.Length+e.Data.Length>1024*1024)overflow=true;else output.AppendLine(e.Data);}};
                process.OutputDataReceived+=read;process.ErrorDataReceived+=(s,e)=>{if(e.Data==null)return;lock(sync){if(error.Length+e.Data.Length>128*1024)overflow=true;else error.AppendLine(e.Data);}};
                process.Start();process.BeginOutputReadLine();process.BeginErrorReadLine();var clock=Stopwatch.StartNew();
                try
                {
                    while(!process.WaitForExit(50))
                    {
                        cancellation.ThrowIfCancellationRequested();
                        if(overflow||clock.Elapsed>TimeSpan.FromSeconds(20))throw new InvalidDataException("The reader check did not finish within its bounds. Choose a compatible DolphinTool.");
                    }
                    process.WaitForExit();cancellation.ThrowIfCancellationRequested();
                    if(overflow||json&&process.ExitCode!=0)throw new InvalidDataException("DolphinTool could not read this disc header. Choose a compatible reader and Brawl image.");
                    // Top-level Dolphin usage can exit nonzero even for help.
                    return json?output.ToString():output.ToString()+Environment.NewLine+error.ToString();
                }
                catch{if(!process.HasExited)process.Kill();throw;}
            }
        }
        public static string RunDolphin(string tool,string arguments,CancellationToken cancellation)
        {
            tool=AnimationLibrary.OrdinaryPath(tool);AnimationLibrary.NoLinks(tool);
            if(!File.Exists(tool)||!string.Equals(System.IO.Path.GetFileName(tool),"DolphinTool.exe",StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("Select an installed DolphinTool.exe with extract/list support.");
            var output=new StringBuilder();var error=new StringBuilder();bool overflow=false;var sync=new object();
            using(var process=new Process{StartInfo=new ProcessStartInfo(tool,arguments){UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=true,RedirectStandardError=true,WorkingDirectory=System.IO.Path.GetDirectoryName(tool)}})
            {
                DataReceivedEventHandler read=(s,e)=>{if(e.Data==null)return;lock(sync){var buffer=ReferenceEquals(s,process)?output:error;if(buffer.Length+e.Data.Length>8*1024*1024){overflow=true;return;}buffer.AppendLine(e.Data);}};
                process.OutputDataReceived+=read;process.ErrorDataReceived+=(s,e)=>{if(e.Data==null)return;lock(sync){if(error.Length+e.Data.Length>1024*1024){overflow=true;return;}error.AppendLine(e.Data);}};
                process.Start();process.BeginOutputReadLine();process.BeginErrorReadLine();var clock=Stopwatch.StartNew();
                try
                {
                    while(!process.WaitForExit(100))
                    {cancellation.ThrowIfCancellationRequested();if(overflow||clock.Elapsed.TotalMinutes>15)throw new InvalidDataException("The bounded disc-source operation did not finish.");}
                    process.WaitForExit();cancellation.ThrowIfCancellationRequested();
                    if(process.ExitCode!=0||overflow)throw new InvalidDataException("Dolphin could not read this source. Select a modern DolphinTool supporting header --json and extract --list/--single. "+error.ToString().Substring(0,Math.Min(1800,error.Length)));
                    return output.ToString();
                }
                catch{if(!process.HasExited)process.Kill();throw;}
            }
        }
        // Ordinary Windows paths cannot contain quotes. Reject them rather than
        // interpolating arbitrary command text or invoking a shell.
        private static string Quote(string value)
        {if(value==null||value.Contains('"')||value.Contains('\r')||value.Contains('\n'))throw new InvalidDataException("Invalid native tool argument.");return "\""+value.TrimEnd('\\')+"\"";}
        private static string[] DiscFiles(LibraryGameRequest request,CancellationToken cancellation)
        {
            string inventory=RunDolphin(request.DolphinToolPath,"extract -i "+Quote(request.Path)+" -p DATA -s fighter -l",cancellation);
            var paths=inventory.Split(new[]{'\r','\n'},StringSplitOptions.RemoveEmptyEntries).Where(p=>p.StartsWith("fighter/",StringComparison.Ordinal)&&!p.EndsWith("/",StringComparison.Ordinal)).ToArray();
            if(paths.Length<1||paths.Length>10000||paths.Any(p=>p.Contains("..")||p.Contains('\\')||p.Contains(':')||p.Split('/').Length<2||p.Split('/').Any(part=>string.IsNullOrWhiteSpace(part))))
                throw new InvalidDataException("The image has no bounded canonical Brawl fighter filesystem.");
            return paths;
        }
        private static string FighterFolder(string source)
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
                if(!new[]{".iso",".wbfs",".rvz"}.Contains(System.IO.Path.GetExtension(path).ToLowerInvariant()))throw new NotSupportedException("Choose ISO, WBFS or RVZ.");
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
        public static LibraryGamePlan Prepare(LibraryGameRequest request,string cacheRoot,CancellationToken cancellation=default,Action<int,string> progress=null)
        {
            cancellation.ThrowIfCancellationRequested();
            if(request==null||!new[]{"Brawl","Project M","Project+"}.Contains(request.Game))throw new NotSupportedException("Whole-build native indexing currently supports Brawl-family sources. Other game paths can be saved; their native adapters are pending.");
            if(!request.ApproveBuild||string.IsNullOrWhiteSpace(request.GameVersion))throw new InvalidDataException("Confirm the selected original game/build before indexing its complete roster.");
            if(request.Characters==null||request.Characters.Length==0||request.Characters.Length>256||request.Characters.Any(c=>!Regex.IsMatch(c??"",@"^[A-Za-z0-9_-]+$")))throw new InvalidDataException("Select one or more characters from the native source browser.");
            string sourcePath=AnimationLibrary.OrdinaryPath(request.Path);AnimationLibrary.NoLinks(sourcePath);
            var plan=new LibraryGamePlan{Source=new LibraryGameLocation{Id=Guid.NewGuid().ToString("N"),Game=request.Game,GameVersion=request.GameVersion,Path=sourcePath,Indexed=true,IsDirectory=Directory.Exists(sourcePath)}};
            string fighterRoot;string originHash=null;long originBytes=0,originTicks=0;
            FileStream sourceLock=null;
            try
            {
                if(File.Exists(sourcePath))
                {
                    if(!new[]{".iso",".wbfs",".rvz"}.Contains(System.IO.Path.GetExtension(sourcePath).ToLowerInvariant()))throw new NotSupportedException("Select a Brawl ISO/WBFS/RVZ image or an extracted Brawl-family folder.");
                    ValidateDiscSource(request,cancellation);
                    sourceLock=new FileStream(sourcePath,FileMode.Open,FileAccess.Read,FileShare.Read);
                    if(sourceLock.Length<=0||sourceLock.Length>64L*1024*1024*1024)throw new InvalidDataException("Disc source exceeds the 64 GB bound.");
                    progress?.Invoke(1,"Hashing original disc image (read-only)...");originHash=HashDisc(sourcePath,cancellation);originBytes=sourceLock.Length;originTicks=File.GetLastWriteTimeUtc(sourcePath).Ticks;
                    string tool=AnimationLibrary.OrdinaryPath(request.DolphinToolPath);plan.Source.ToolPath=tool;plan.Source.ToolSha256=FighterProject.HashFile(tool);
                    var header=new JavaScriptSerializer().Deserialize<Dictionary<string,object>>(RunDolphin(tool,"header -i "+Quote(sourcePath)+" -j",cancellation));
                    if(header==null||!header.TryGetValue("game_id",out var gameId)||!header.TryGetValue("revision",out var revision)||!Regex.IsMatch(gameId.ToString(),@"^RSB[A-Z0-9]{3}$"))
                        throw new InvalidDataException("This is not an identified Brawl-family disc. Select the correct game source adapter.");
                    plan.Source.NativeGameId=gameId.ToString();plan.Source.NativeRevision=Convert.ToInt32(revision);plan.Source.NativeRegion=header.TryGetValue("region",out var region)?region.ToString():"Unknown";
                    plan.Source.GameVersion=request.GameVersion+" · "+plan.Source.NativeGameId+" revision "+plan.Source.NativeRevision;
                    progress?.Invoke(3,"Listing every fighter in the disc DATA partition...");
                    var paths=DiscFiles(request,cancellation);
                    if(request.Characters.Any(c=>!paths.Any(p=>p.Split('/')[1]==c)))throw new InvalidDataException("A selected character is not present in this image.");
                    paths=paths.Where(p=>request.Characters.Contains(p.Split('/')[1])).ToArray();
                    plan.Source.InventorySha256=Digest(string.Join("\n",paths));plan.Source.Provider="dolphin-brawl-disc-v1";
                    // A fresh snapshot never overwrites another live library lease.
                    string cache=System.IO.Path.Combine(AnimationLibrary.OrdinaryPath(cacheRoot),Guid.NewGuid().ToString("N"));AnimationLibrary.NoLinks(cache);
                    if(paths.Any(p=>System.IO.Path.Combine(cache,"DATA","files",p.Replace('/','\\')).Length>=250))throw new InvalidDataException("Choose a shorter shared-library directory; a selected native cache path exceeds the external tool's safe Windows path length.");
                    fighterRoot=System.IO.Path.Combine(cache,"DATA","files","fighter");
                    foreach(string character in request.Characters.Distinct())
                    {
                        progress?.Invoke(5,"Reading selected character: "+character+" (original image is read-only)...");
                        Directory.CreateDirectory(cache);RunDolphin(tool,"extract -i "+Quote(sourcePath)+" -p DATA -s "+Quote("fighter/"+character)+" -o "+Quote(cache)+" -q",cancellation);
                    }
                    if(!paths.All(p=>File.Exists(System.IO.Path.Combine(cache,"DATA","files",p.Replace('/',System.IO.Path.DirectorySeparatorChar)))))throw new InvalidDataException("Selective disc extraction did not produce every listed fighter file.");
                    plan.Source.ContainerSha256=originHash;plan.Source.Bytes=originBytes;plan.Source.Label=System.IO.Path.GetFileNameWithoutExtension(sourcePath);
                }
                else
                {
                    if(!Directory.Exists(sourcePath))throw new DirectoryNotFoundException("Choose an existing extracted game/build folder.");
                    fighterRoot=FighterFolder(sourcePath);
                    plan.Source.Provider="brawl-folder-v1";plan.Source.Label=new DirectoryInfo(sourcePath).Name;
                }
                var files=request.Characters.Distinct().SelectMany(c=>NativeFiles(System.IO.Path.Combine(fighterRoot,c),cancellation)).OrderBy(p=>p,StringComparer.OrdinalIgnoreCase).Take(10001).ToArray();
                if(files.Length==0||files.Length>10000)throw new InvalidDataException("Native source file inventory exceeds its bounded limit or is empty.");
                long totalBytes=0;var models=new List<ModelFacts>();var clips=new List<ClipFacts>();
                foreach(string path in files)
                {
                    cancellation.ThrowIfCancellationRequested();AnimationLibrary.NoLinks(path);var info=new FileInfo(path);totalBytes+=info.Length;
                    if(totalBytes>4L*1024*1024*1024)throw new InvalidDataException("The selected native fighter subtree exceeds the 4 GB cache/indexing bound.");
                    string relative="fighter/"+path.Substring(fighterRoot.Length+1).Replace('\\','/');string character=relative.Split('/')[1];
                    var mount=new LibraryMount{Id=Guid.NewGuid().ToString("N"),Game=request.Game,GameVersion=plan.Source.GameVersion,LocalPath=path,ContainerSha256=FighterProject.HashFile(path),Bytes=info.Length,
                        BuildEvidence="User-confirmed game/build; "+plan.Source.Provider+"; native resource indices, source hashes and body-track binding checked",GameSourceId=plan.Source.Id,VirtualPath=relative,
                        OriginPath=originHash==null?null:sourcePath,OriginSha256=originHash,OriginBytes=originBytes,OriginWriteTicks=originTicks,OriginPartition=originHash==null?null:"DATA"};plan.Mounts.Add(mount);
                    progress?.Invoke(10+plan.Mounts.Count*75/files.Length,"Indexing "+plan.Mounts.Count+" / "+files.Length+" native fighter packages: "+relative);
                    using(var held=AnimationLibrary.LockSource(path))
                    using(var root=NodeFactory.FromFile(null,path))
                    {
                        if(root==null){plan.Issues.Add(relative+": unsupported native package");continue;}
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
                                    Tags=character+" "+request.Game,AssociationIssue="Original source model requires verified association"};
                                clips.Add(new ClipFacts{Clip=metadata,Character=character,Bones=motion is CHR0Node?motion.Children.Select(n=>n.Name).ToArray():new string[0]});plan.Clips.Add(metadata);
                            }
                        }
                    }
                }
                // Native costume-00 conventions provide candidates, not approval
                // by name. Build review + native ownership + exact body binding
                // select a canonical original. Article ambiguity stays unresolved.
                foreach(var clip in clips)
                {
                    cancellation.ThrowIfCancellationRequested();
                    if(clip.Clip.Channel!="CHR0 skeletal"||(clip.Clip.NativeVersion!=4&&clip.Clip.NativeVersion!=5)||clip.Clip.FrameCount>4096){clip.Clip.AssociationIssue="Unsupported channel/version";continue;}
                    var candidates=models.Where(m=>m.Model.Character==clip.Character&&m.Model.HasGeometry&&!m.Model.Shadow&&Regex.IsMatch(System.IO.Path.GetFileName(m.Path),@"^Fit.+00\.(pac|pcs)$",RegexOptions.IgnoreCase)&&
                        clip.Bones.Length>0&&clip.Bones.Count(b=>!m.Bones.Contains(b))<=clip.Bones.Length/10&&clip.Bones.Count(m.Body.Contains)>=2).ToArray();
                    if(candidates.Length==0)continue;
                    if(candidates.Select(c=>c.Model.ResourceSha256).Distinct().Count()!=1)continue;
                    var chosen=candidates[0];
                    clip.Clip.ModelId=chosen.Model.Id;clip.Clip.AssociationIssue=null;
                    var unbound=clip.Bones.Where(b=>!chosen.Bones.Contains(b)).ToArray();clip.Clip.UnboundSourceTracks=unbound.Length==0?null:string.Join(", ",unbound);
                    clip.Clip.AssociationEvidence="User-confirmed original game/build; native fighter-folder ownership and unique costume-00 body; exact container/resource and skeleton hashes; major body roles and at least 90% of native tracks bind; unbound auxiliary tracks explicitly reported";
                    if(!chosen.Model.MotionMountIds.Contains(clip.Clip.MountId))chosen.Model.MotionMountIds.Add(clip.Clip.MountId);
                }
                if(plan.Source.InventorySha256==null)plan.Source.InventorySha256=Digest(string.Join("\n",plan.Mounts.Select(m=>m.VirtualPath+":"+m.ContainerSha256)));
                plan.Source.NativeFiles=files.Length;if(plan.Source.ContainerSha256==null)plan.Source.ContainerSha256=plan.Source.InventorySha256;
                if(plan.Models.Count==0||plan.Clips.Count==0||plan.Clips.Count>AnimationLibrary.MaximumClips)throw new InvalidDataException("No supported model/motion inventory was found, or the source exceeds catalogue limits.");
                progress?.Invoke(100,"Complete roster indexed: "+plan.Models.Count+" models, "+plan.Clips.Count+" animation resources.");return plan;
            }
            finally{sourceLock?.Dispose();}
        }
    }
}
