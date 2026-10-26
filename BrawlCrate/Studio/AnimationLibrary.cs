using BrawlCrate.Studio.Retargeting;
using BrawlLib.SSBB.ResourceNodes;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Xml;
using System.Xml.Serialization;

namespace BrawlCrate.Studio
{
    public sealed class LibrarySearchItem
    {
        public LibraryAnimation Clip { get; internal set; }
        public LibraryModel Model { get; internal set; }
        public LibraryMount Motion { get; internal set; }
        public LibrarySourceState State { get; internal set; }
    }

    /// <summary>One bounded metadata catalogue, independent of every fighter.</summary>
    public sealed class AnimationLibrary
    {
        public static int DiagnosticOpens;
        public const int MaximumClips=50000;
        public static readonly string[] PlannedGames={"Brawl","Project M","Project+","Melee","Ultimate","Rivals II","FighterZ","Sparking! ZERO","Xenoverse 1","Xenoverse 2"};
        private const long MaximumIndexBytes=64L*1024*1024;
        public string DirectoryPath { get; }
        // Publish a new immutable generation with a same-directory rename. This
        // also works on encrypted Windows profiles where ReplaceFile is denied.
        public string IndexPath=>Generations().FirstOrDefault()??Path.Combine(DirectoryPath,"catalogue.xml");
        private string PreviousPath=>Path.Combine(DirectoryPath,"catalogue.previous.xml");
        private string[] Generations()
        {
            if(!Directory.Exists(DirectoryPath))return new string[0];
            var files=Directory.EnumerateFiles(DirectoryPath,"catalogue-*.xml").Take(129).ToArray();
            if(files.Length>128)throw new InvalidDataException("Too many library generations. Preserve the catalogue directory and restore its last verified generation.");
            if(files.Any(p=>{string name=Path.GetFileNameWithoutExtension(p);return name.Length!=30||!name.Substring(10).All(char.IsDigit);}))
                throw new InvalidDataException("Invalid library generation filename; metadata has been preserved.");
            return files.OrderByDescending(p=>p,StringComparer.Ordinal).ToArray();
        }
        private string PriorIndex()
        {
            var files=Generations();if(files.Length>1)return files[1];
            string legacy=Path.Combine(DirectoryPath,"catalogue.xml");
            return files.Length==1&&File.Exists(legacy)?legacy:File.Exists(PreviousPath)?PreviousPath:null;
        }
        private readonly Dictionary<string,Tuple<long,long,LibrarySourceState>> health=new Dictionary<string,Tuple<long,long,LibrarySourceState>>();
        private LibraryCatalogue searchSnapshot;
        private sealed class IndexedClip
        {internal LibraryAnimation Clip;internal LibraryModel Model;internal LibraryMount Motion;internal string Text,GameText,ModelText;}
        private LibraryCatalogue indexedCatalogue;
        private IndexedClip[] indexedClips;
        private Dictionary<string,LibraryModel> indexedModels;
        private Dictionary<string,LibraryMount> indexedMounts;
        private readonly object cacheSync=new object();
        private LibraryCatalogue costumeCatalogue;
        private readonly Dictionary<string,LibraryCostumeOption[]> costumeChoices=new Dictionary<string,LibraryCostumeOption[]>(StringComparer.Ordinal);
        private string snapshotPath;
        private long snapshotTicks, snapshotBytes;
        // Browsing never reparses the complete catalogue on each keystroke.
        // Writers and conversions still read an independently validated copy.
        private LibraryCatalogue SearchSnapshot()
        {
            lock(cacheSync)
            {
            string path=IndexPath;var file=new FileInfo(path);
            long ticks=file.Exists?file.LastWriteTimeUtc.Ticks:0,bytes=file.Exists?file.Length:0;
            if(searchSnapshot==null||snapshotPath!=path||snapshotTicks!=ticks||snapshotBytes!=bytes)
            {searchSnapshot=file.Exists?ReadFile(path):new LibraryCatalogue();snapshotPath=path;snapshotTicks=ticks;snapshotBytes=bytes;}
            return searchSnapshot;
            }
        }
        private readonly Lazy<LibraryFavorites> favorites;
        public bool IsFavorite(string clipId) => favorites.Value.Contains(clipId);
        public string FavoritePreferenceError => favorites.Value.LastError;
        public string[] Channels => SearchSnapshot().Clips.Select(c=>c.Channel).Distinct().OrderBy(c=>c,StringComparer.Ordinal).ToArray();
        public void SetFavorite(string clipId,bool favorite)
        {
            if(!SearchSnapshot().Clips.Any(c=>c.Id==clipId)) throw new InvalidDataException("This exact library clip is no longer registered.");
            favorites.Value.Set(clipId,favorite);
        }
        public int ClipCount=>SearchSnapshot().Clips.Count;
        public string[] ConfiguredGames=>SearchSnapshot().GameLocations.Select(l=>l.Game).Distinct().ToArray();
        public static string DefaultDirectory=>Environment.GetEnvironmentVariable("FIGHTERPORT_LIBRARY_ROOT")??
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"FighterPortStudioV2","AnimationLibrary");
        public AnimationLibrary(string directory=null)
        {
            DirectoryPath=OrdinaryPath(directory??DefaultDirectory);NoLinks(DirectoryPath);
            favorites=new Lazy<LibraryFavorites>(()=>new LibraryFavorites(DirectoryPath));
        }
        internal static string OrdinaryPath(string path)
        {
            if(string.IsNullOrWhiteSpace(path))throw new InvalidDataException("Choose an ordinary local source path.");
            string full=Path.GetFullPath(path);
            if(full.StartsWith("\\\\",StringComparison.Ordinal) || full.IndexOf(':',2)>=0)throw new InvalidDataException("The library accepts ordinary local drive paths.");
            return full;
        }
        internal static void NoLinks(string path)
        {
            for(string current=Path.GetFullPath(path);!string.IsNullOrEmpty(current);current=Path.GetDirectoryName(current))
                if((File.Exists(current)||Directory.Exists(current)) && (File.GetAttributes(current)&FileAttributes.ReparsePoint)!=0)
                    throw new InvalidDataException("Linked library/source paths are not supported.");
        }
        internal static FileStream LockSource(string path)
        {
            path=OrdinaryPath(path);NoLinks(path);
            var file=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.Read);
            if(file.Length<=0 || file.Length>FighterProject.MaximumAssetBytes){file.Dispose();throw new InvalidDataException("Source assets must contain data and be no larger than 1 GB.");}
            return file;
        }
        public LibraryCatalogue Read()=>File.Exists(IndexPath)?ReadFile(IndexPath):new LibraryCatalogue();
        private static LibraryCatalogue ReadFile(string path)
        {
            NoLinks(path);
            if(new FileInfo(path).Length>MaximumIndexBytes)throw new InvalidDataException("The shared library index exceeds its bounded size limit.");
            try
            {
                using(var reader=XmlReader.Create(path,new XmlReaderSettings{DtdProcessing=DtdProcessing.Prohibit,XmlResolver=null,MaxCharactersInDocument=MaximumIndexBytes}))
                {
                    var data=(LibraryCatalogue)new XmlSerializer(typeof(LibraryCatalogue)).Deserialize(reader);Validate(data);
                    if(Path.GetFileName(path).StartsWith("catalogue-",StringComparison.Ordinal)&&Path.GetFileNameWithoutExtension(path)!=
                        "catalogue-"+data.Generation.ToString("D20",System.Globalization.CultureInfo.InvariantCulture))throw new InvalidDataException("Catalogue generation identity differs from its publication name.");
                    return data;
                }
            }
            catch(Exception e) when(e is XmlException || e is InvalidOperationException)
            {throw new InvalidDataException("The library index is malformed; it has been preserved. Restore its previous copy before adding sources.",e);}
        }
        private static bool Id(string value)=>Guid.TryParseExact(value,"N",out _);
        private static bool Hash(string value)=>value!=null && value.Length==64 && value.All(Uri.IsHexDigit);
        private static bool Text(string value,int length=8192)=>!string.IsNullOrWhiteSpace(value)&&value.Length<=length;
        private static void Validate(LibraryCatalogue data)
        {
            if(data==null || data.Schema!=1 || data.Generation<0 || data.Mounts==null || data.Models==null || data.Clips==null ||
                data.Mounts.Count>8192 || data.Models.Count>8192 || data.Clips.Count>MaximumClips)throw new InvalidDataException("Unsupported or oversized library index.");
            if(data.Mounts.Any(m=>m==null || !Id(m.Id)||!Hash(m.ContainerSha256)||!Text(m.LocalPath,32767)||!Text(m.Game,128)||!Text(m.GameVersion,256)||
                !Text(m.Platform,128)||!Text(m.Provider,128)||!Text(m.BuildEvidence)||m.Bytes<=0||m.Bytes>FighterProject.MaximumAssetBytes) ||
                data.Mounts.Select(m=>m.Id).Distinct().Count()!=data.Mounts.Count)throw new InvalidDataException("Invalid source mount identity.");
            foreach(var mount in data.Mounts)OrdinaryPath(mount.LocalPath);
            var mounts=data.Mounts.ToDictionary(m=>m.Id);
            if(data.Models.Any(m=>m==null || !Id(m.Id)||!mounts.ContainsKey(m.MountId??"")||!Text(m.Name,512)||!Text(m.Character,256)||!Text(m.Variant,512)||
                !Text(m.ResourceKey,512)||!Text(m.ResourcePath)||!Hash(m.ResourceSha256)||!Hash(m.SkeletonSha256)||!Text(m.RestInterpretation,128)) ||
                data.Models.Select(m=>m.Id).Distinct().Count()!=data.Models.Count)throw new InvalidDataException("Invalid source model/skeleton identity.");
            var models=data.Models.ToDictionary(m=>m.Id);
            if(data.Models.Any(m=>m.MotionMountIds==null||m.MotionMountIds.Count>8192||m.MotionMountIds.Any(id=>!mounts.ContainsKey(id))))throw new InvalidDataException("Invalid source-set motion ownership.");
            if(data.GameLocations==null||data.GameLocations.Count>256||data.GameLocations.Any(l=>l==null||!Id(l.Id)||!Text(l.Game,128)||!Text(l.GameVersion,256)||!Text(l.Path,32767)||!Text(l.Label,512))||
                data.GameLocations.Select(l=>l.Id).Distinct().Count()!=data.GameLocations.Count)throw new InvalidDataException("Invalid game/build path configuration.");
            foreach(var location in data.GameLocations)OrdinaryPath(location.Path);
            foreach(var location in data.GameLocations.Where(l=>l.Indexed))
                if(!Hash(location.ContainerSha256)||!Hash(location.InventorySha256)||location.NativeFiles<1||
                    (location.Provider!="brawl-folder-v1"&&!BrawlGameSource.IsDiscProvider(location.Provider))||
                    (location.Provider=="dolphin-brawl-disc-v1"&&(!Hash(location.ToolSha256)||!Text(location.ToolPath,32767)||location.Bytes<=0||location.Bytes>64L*1024*1024*1024))||
                    (location.Provider==BrawlDiscImage.Provider&&(location.ToolPath!=null||location.ToolSha256!=null||location.Bytes<=0||location.Bytes>64L*1024*1024*1024)))
                    throw new InvalidDataException("Invalid indexed game/build provenance.");
            foreach(var mount in data.Mounts.Where(m=>m.GameSourceId!=null))
            {
                if(!data.GameLocations.Any(l=>l.Id==mount.GameSourceId&&l.Indexed)||!Text(mount.VirtualPath)||mount.VirtualPath.StartsWith("/",StringComparison.Ordinal)||mount.VirtualPath.Contains("..")||mount.VirtualPath.Contains('\\')||mount.VirtualPath.Contains(':'))
                    throw new InvalidDataException("Invalid game-source virtual identity.");
                if(mount.OriginPath!=null)
                {
                    OrdinaryPath(mount.OriginPath);
                    if(!Hash(mount.OriginSha256)||mount.OriginBytes<=0||mount.OriginBytes>64L*1024*1024*1024||mount.OriginWriteTicks<=0)
                        throw new InvalidDataException("Invalid original disc identity.");
                    var origin=data.GameLocations.Single(l=>l.Id==mount.GameSourceId);
                    if(origin.ContainerSha256!=mount.OriginSha256||origin.Bytes!=mount.OriginBytes||!BrawlGameSource.IsDiscProvider(origin.Provider))throw new InvalidDataException("Disc resource origin differs from its source mount.");
                }
            }
            if(data.Clips.Any(c=>c==null || !Id(c.Id)||c.Revision<1||!mounts.ContainsKey(c.MountId??"")||!Text(c.Name,512)||!Text(c.ResourceKey,512)||!Text(c.ResourcePath)||
                !Hash(c.ResourceSha256)||!Text(c.Channel,128)||c.FrameCount<0||c.FrameCount>10000000||c.TicksPerSecond!=60||!Text(c.TimebaseEvidence)||!Text(c.Interpolation)||
                (c.ModelId!=null && (!models.ContainsKey(c.ModelId)||!Text(c.AssociationEvidence)))) ||
                data.Clips.Select(c=>c.Id).Distinct().Count()!=data.Clips.Count)throw new InvalidDataException("Invalid source animation identity or association.");
            foreach(var c in data.Clips.Where(c=>c.ModelId!=null))
            {
                var a=mounts[c.MountId];var b=mounts[models[c.ModelId].MountId];
                if(a.Game!=b.Game || a.GameVersion!=b.GameVersion || a.Platform!=b.Platform || a.Provider!=b.Provider)
                    throw new InvalidDataException("A source-model association crosses incompatible game/build interpretations.");
            }
        }
        private T Update<T>(Func<LibraryCatalogue,Tuple<T,bool>> change,CancellationToken cancellation=default)
        {
            NoLinks(DirectoryPath);Directory.CreateDirectory(DirectoryPath);
            string identity;
            using(var sha=System.Security.Cryptography.SHA256.Create())identity=BrawlLibrarySource.Hex(sha.ComputeHash(System.Text.Encoding.UTF8.GetBytes(DirectoryPath.ToUpperInvariant())));
            using(var mutex=new Mutex(false,"Local\\FighterPortLibrary-"+identity))
            {
                bool held=false;
                try
                {
                    try{held=mutex.WaitOne(5000);}catch(AbandonedMutexException){held=true;}
                    if(!held)throw new IOException("The shared library is busy in another editor. Try again.");
                    cancellation.ThrowIfCancellationRequested();var current=Read();var changed=change(current);
                    if(!changed.Item2)return changed.Item1;
                    current.Generation++;Validate(current);
                    string temporary=Path.Combine(DirectoryPath,".library-"+Guid.NewGuid().ToString("N")+".tmp");
                    try
                    {
                        NoLinks(IndexPath);
                        using(var stream=new FileStream(temporary,FileMode.CreateNew,FileAccess.Write,FileShare.None))
                        {
                            using(var writer=XmlWriter.Create(stream,new XmlWriterSettings{Indent=true,Encoding=new System.Text.UTF8Encoding(false),CloseOutput=false}))
                                new XmlSerializer(typeof(LibraryCatalogue)).Serialize(writer,current);
                            if(stream.Length>MaximumIndexBytes)throw new InvalidDataException("The shared library index exceeds 64 MB.");
                            stream.Flush(true);
                        }
                        cancellation.ThrowIfCancellationRequested();
                        string published=Path.Combine(DirectoryPath,"catalogue-"+current.Generation.ToString("D20",System.Globalization.CultureInfo.InvariantCulture)+".xml");
                        NoLinks(published);File.Move(temporary,published);
                        // Retain current + two recovery generations. Cleanup is
                        // optional: a failure never invalidates a committed file.
                        foreach(string old in Generations().Skip(3))
                            try{File.Delete(old);}catch(IOException){}catch(UnauthorizedAccessException){}
                    }
                    finally{if(File.Exists(temporary))File.Delete(temporary);}
                    lock(cacheSync){health.Clear();searchSnapshot=null;}return changed.Item1;
                }
                finally{if(held)mutex.ReleaseMutex();}
            }
        }
        public int Register(LibraryImportPlan plan,CancellationToken cancellation=default)
        {
            if(plan==null)throw new ArgumentNullException(nameof(plan));
            using(var modelLock=LockSource(plan.ModelMount.LocalPath))using(var motionLock=LockSource(plan.MotionMount.LocalPath))
            {
                if(FighterProject.HashFile(plan.ModelMount.LocalPath)!=plan.ModelMount.ContainerSha256 || FighterProject.HashFile(plan.MotionMount.LocalPath)!=plan.MotionMount.ContainerSha256)
                    throw new InvalidDataException("A source changed during inventory. No library entries were committed.");
                plan.ModelMount.NativeWriteTicks=File.GetLastWriteTimeUtc(plan.ModelMount.LocalPath).Ticks;
                plan.MotionMount.NativeWriteTicks=File.GetLastWriteTimeUtc(plan.MotionMount.LocalPath).Ticks;
                return Update(data=>{
                    int added=0;bool changed=false;
                    Func<LibraryMount,LibraryMount> mount=input=>{
                        var old=data.Mounts.FirstOrDefault(m=>string.Equals(m.LocalPath,input.LocalPath,StringComparison.OrdinalIgnoreCase)&&m.ContainerSha256==input.ContainerSha256&&m.Game==input.Game&&m.GameVersion==input.GameVersion);
                        if(old!=null){if(old.NativeWriteTicks!=input.NativeWriteTicks){old.NativeWriteTicks=input.NativeWriteTicks;changed=true;}return old;}data.Mounts.Add(input);changed=true;return input;
                    };
                    var a=mount(plan.ModelMount);var b=mount(plan.MotionMount);
                    var model=data.Models.FirstOrDefault(m=>m.MountId==a.Id&&m.ResourceKey==plan.Model.ResourceKey&&m.ResourceSha256==plan.Model.ResourceSha256);
                    if(model==null){model=plan.Model;model.MountId=a.Id;data.Models.Add(model);changed=true;}
                    if(!model.MotionMountIds.Contains(b.Id)){model.MotionMountIds.Add(b.Id);changed=true;}
                    Func<LibraryAnimation,string> key=c=>c.MountId+"|"+c.ResourceKey+"|"+c.ResourceSha256+"|"+c.ModelId;
                    var indexed=new HashSet<string>(data.Clips.Select(key),StringComparer.Ordinal);
                    foreach(var clip in plan.Clips)
                    {
                        cancellation.ThrowIfCancellationRequested();string modelId=clip.ModelId==null?null:model.Id;
                        clip.MountId=b.Id;clip.ModelId=modelId;
                        if(!indexed.Add(key(clip)))continue;
                        data.Clips.Add(clip);added++;changed=true;
                    }
                    if(changed)data.LastOperation="Add Source";
                    return Tuple.Create(added,changed);
                },cancellation);
            }
        }
        public int RegisterGame(LibraryGamePlan plan,CancellationToken cancellation=default)
        {
            if(plan==null)throw new ArgumentNullException(nameof(plan));
            foreach(var mount in plan.Mounts)
            {
                cancellation.ThrowIfCancellationRequested();
                using(var held=LockSource(mount.LocalPath))
                {
                    if(held.Length!=mount.Bytes||FighterProject.HashFile(mount.LocalPath)!=mount.ContainerSha256)throw new InvalidDataException("A native source changed during indexing. Nothing was registered.");
                    mount.NativeWriteTicks=File.GetLastWriteTimeUtc(mount.LocalPath).Ticks;
                }
            }
            return Update(data=>{
                bool changed=false;
                var location=data.GameLocations.FirstOrDefault(l=>l.Indexed&&l.Game==plan.Source.Game&&l.GameVersion==plan.Source.GameVersion&&l.ContainerSha256==plan.Source.ContainerSha256&&l.Provider==plan.Source.Provider&&l.ToolSha256==plan.Source.ToolSha256);
                if(location==null){location=plan.Source;data.GameLocations.Add(location);changed=true;}
                var mounts=new Dictionary<string,string>();
                foreach(var input in plan.Mounts)
                {
                    string originalId=input.Id;
                    var old=data.Mounts.FirstOrDefault(m=>m.GameSourceId==location.Id&&m.VirtualPath==input.VirtualPath&&m.ContainerSha256==input.ContainerSha256);
                    if(old==null){input.GameSourceId=location.Id;data.Mounts.Add(input);old=input;changed=true;}
                    else if(!File.Exists(old.LocalPath)||FighterProject.HashFile(old.LocalPath)!=old.ContainerSha256){old.LocalPath=input.LocalPath;old.NativeWriteTicks=input.NativeWriteTicks;changed=true;}
                    mounts.Add(originalId,old.Id);
                }
                var models=new Dictionary<string,string>();
                foreach(var input in plan.Models)
                {
                    string originalId=input.Id,mount=mounts[input.MountId];
                    var old=data.Models.FirstOrDefault(m=>m.MountId==mount&&m.ResourceKey==input.ResourceKey&&m.ResourceSha256==input.ResourceSha256);
                    if(old==null){input.MountId=mount;input.MotionMountIds=input.MotionMountIds.Select(id=>mounts[id]).Distinct().ToList();data.Models.Add(input);old=input;changed=true;}
                    else foreach(string id in input.MotionMountIds.Select(id=>mounts[id]))if(!old.MotionMountIds.Contains(id)){old.MotionMountIds.Add(id);changed=true;}
                    models.Add(originalId,old.Id);
                }
                int added=0;var keys=new HashSet<string>(data.Clips.Select(c=>c.MountId+"|"+c.ResourceKey+"|"+c.ResourceSha256+"|"+c.ModelId));
                foreach(var clip in plan.Clips)
                {
                    cancellation.ThrowIfCancellationRequested();clip.MountId=mounts[clip.MountId];if(clip.ModelId!=null)clip.ModelId=models[clip.ModelId];
                    if(keys.Add(clip.MountId+"|"+clip.ResourceKey+"|"+clip.ResourceSha256+"|"+clip.ModelId)){data.Clips.Add(clip);added++;changed=true;}
                }
                if(changed)data.LastOperation="Index Game Source";return Tuple.Create(added,changed);
            },cancellation);
        }
        public void RepairGameSource(string sourceId,string replacement,CancellationToken cancellation=default)
        {
            string path=OrdinaryPath(replacement);NoLinks(path);
            using(var held=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.Read))
            {
                if(held.Length<=0||held.Length>64L*1024*1024*1024)throw new InvalidDataException("Invalid disc image size.");
                string hash=BrawlGameSource.HashDisc(path,cancellation);
                Update(data=>{
                    var source=data.GameLocations.Single(l=>l.Id==sourceId);
                    if(!source.Indexed||!BrawlGameSource.IsDiscProvider(source.Provider)||source.ContainerSha256!=hash||source.Bytes!=held.Length)throw new InvalidDataException("Select the exact indexed disc image. Its hash/size must match.");
                    source.Path=path;foreach(var mount in data.Mounts.Where(m=>m.GameSourceId==sourceId)){mount.OriginPath=path;mount.OriginWriteTicks=File.GetLastWriteTimeUtc(path).Ticks;}
                    data.LastOperation="Repair Disc Source";return Tuple.Create(true,true);
                },cancellation);
            }
        }
        public void Repair(string mountId,string replacementPath,CancellationToken cancellation=default)
        {
            cancellation.ThrowIfCancellationRequested();
            string path=OrdinaryPath(replacementPath);
            using(var held=LockSource(path))
            {
                string hash=FighterProject.HashFile(path);
                Update(data=>{
                    var mount=data.Mounts.SingleOrDefault(m=>m.Id==mountId)??throw new InvalidDataException("Unknown source mount.");
                    if(hash!=mount.ContainerSha256 || held.Length!=mount.Bytes)throw new InvalidDataException("This is not the exact indexed source. Its hash differs; the association was preserved unchanged.");
                    mount.LocalPath=path;mount.NativeWriteTicks=File.GetLastWriteTimeUtc(path).Ticks;data.LastOperation="Repair Source";return Tuple.Create(true,true);
                },cancellation);
            }
        }
        public bool CanUndo=>SearchSnapshot().Generation>0&&SearchSnapshot().LastOperation!="Undo";
        public void UndoLastUpdate()
        {
            Update(data=>{
                if(data.Generation==0||data.LastOperation=="Undo")throw new InvalidOperationException("No library update is available to undo.");
                string prior=PriorIndex();var previous=prior==null?new LibraryCatalogue():ReadFile(prior);
                data.Mounts=previous.Mounts;data.Models=previous.Models;data.Clips=previous.Clips;data.GameLocations=previous.GameLocations;data.LastOperation="Undo";
                return Tuple.Create(true,true);
            });
        }
        public void RemoveSource(string modelId)
        {
            Update(data=>{
                var model=data.Models.SingleOrDefault(m=>m.Id==modelId)??throw new InvalidDataException("Unknown source model.");
                var owned=new HashSet<string>(model.MotionMountIds.Concat(data.Clips.Where(c=>c.ModelId==modelId).Select(c=>c.MountId)));
                data.Models.Remove(model);
                var shared=new HashSet<string>(data.Models.SelectMany(m=>m.MotionMountIds).Concat(data.Clips.Where(c=>c.ModelId!=null&&c.ModelId!=modelId).Select(c=>c.MountId)));
                data.Clips.RemoveAll(c=>c.ModelId==modelId||c.ModelId==null&&owned.Contains(c.MountId)&&!shared.Contains(c.MountId));
                var retained=new HashSet<string>(data.Models.Select(m=>m.MountId).Concat(data.Models.SelectMany(m=>m.MotionMountIds)).Concat(data.Clips.Select(c=>c.MountId)));
                data.Mounts.RemoveAll(m=>!retained.Contains(m.Id));data.LastOperation="Remove Source";return Tuple.Create(true,true);
            });
        }
        public void SetGameLocation(string game,string version,string path)
        {
            if(!Text(game,128)||!Text(version,256))throw new InvalidDataException("Choose or enter a game and explicit build/version (or mark it unknown).");
            path=OrdinaryPath(path);NoLinks(path);
            if(!File.Exists(path)&&!Directory.Exists(path))throw new FileNotFoundException("Choose an existing game/build folder, disc image or archive.");
            Update(data=>{
                var old=data.GameLocations.FirstOrDefault(l=>!l.Indexed&&l.Game==game&&l.GameVersion==version);
                string label=Directory.Exists(path)?new DirectoryInfo(path).Name:System.IO.Path.GetFileNameWithoutExtension(path);
                if(old!=null){old.Path=path;old.Label=label;old.IsDirectory=Directory.Exists(path);}
                else data.GameLocations.Add(new LibraryGameLocation{Id=Guid.NewGuid().ToString("N"),Game=game,GameVersion=version,Path=path,Label=label,IsDirectory=Directory.Exists(path)});
                data.LastOperation="Set Game Path";return Tuple.Create(true,true);
            });
        }
        public void RemoveGameLocation(string id)
        {
            Update(data=>{if(data.Mounts.Any(m=>m.GameSourceId==id))throw new InvalidOperationException("Remove this build's registered model sources first. Saved target revisions remain independent.");
                if(data.GameLocations.RemoveAll(l=>l.Id==id)==0)throw new InvalidDataException("Unknown game/build location.");
                data.LastOperation="Remove Game Path";return Tuple.Create(true,true);});
        }
        public void ProtectOutputPath(string destination)
        {
            string path=OrdinaryPath(destination);var data=SearchSnapshot();
            if(data.Mounts.Any(m=>string.Equals(path,m.LocalPath,StringComparison.OrdinalIgnoreCase)||string.Equals(path,m.OriginPath,StringComparison.OrdinalIgnoreCase))||
                data.GameLocations.Any(l=>string.Equals(path,l.Path,StringComparison.OrdinalIgnoreCase)||
                    (l.IsDirectory||Directory.Exists(l.Path))&&path.StartsWith(l.Path.TrimEnd('\\','/')+System.IO.Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase)))
                throw new InvalidOperationException("Registered library/game sources are read-only. Choose an output outside the source installation, image or original asset.");
        }
        private LibrarySourceState MountState(LibraryMount mount,bool force=false,Dictionary<string,FileInfo> files=null)
        {
            try
            {
                Func<string,FileInfo> file=path=>{
                    if(files!=null&&files.TryGetValue(path,out var cached))return cached;
                    var current=new FileInfo(path);if(files!=null)files[path]=current;return current;
                };
                if(mount.OriginPath!=null)
                {
                    BrawlGameSource.EnsureSingleDiscSource(mount.OriginPath);
                    var origin=file(mount.OriginPath);if(!origin.Exists)return LibrarySourceState.Missing;
                    if(force)NoLinks(mount.OriginPath);
                    if((origin.Attributes&FileAttributes.ReparsePoint)!=0)return LibrarySourceState.Missing;
                    if(origin.Length!=mount.OriginBytes)return LibrarySourceState.SourceChanged;
                    // A changed image is reverified by the cancellable Repair
                    // operation; browsing never hashes gigabytes on the UI thread.
                    if(origin.LastWriteTimeUtc.Ticks!=mount.OriginWriteTicks)return LibrarySourceState.SourceChanged;
                }
                var info=file(mount.LocalPath);if(!info.Exists)return LibrarySourceState.Missing;
                if(force)NoLinks(mount.LocalPath);
                if((info.Attributes&FileAttributes.ReparsePoint)!=0)return LibrarySourceState.Missing;
                if(info.Length!=mount.Bytes)return LibrarySourceState.SourceChanged;
                if(force)return FighterProject.HashFile(mount.LocalPath)==mount.ContainerSha256?LibrarySourceState.Available:LibrarySourceState.SourceChanged;
                if(mount.NativeWriteTicks>0)return info.LastWriteTimeUtc.Ticks==mount.NativeWriteTicks?LibrarySourceState.Available:LibrarySourceState.SourceChanged;
                string key=mount.Id+mount.ContainerSha256+mount.LocalPath;
                if(!force && health.TryGetValue(key,out var old)&&old.Item1==info.Length&&old.Item2==info.LastWriteTimeUtc.Ticks)return old.Item3;
                var state=FighterProject.HashFile(mount.LocalPath)==mount.ContainerSha256?LibrarySourceState.Available:LibrarySourceState.SourceChanged;
                health[key]=Tuple.Create(info.Length,info.LastWriteTimeUtc.Ticks,state);return state;
            }
            catch(Exception e) when(e is IOException || e is UnauthorizedAccessException || e is InvalidDataException){return LibrarySourceState.Missing;}
        }
        private LibrarySourceState CachedMountState(LibraryMount mount,bool force,Dictionary<string,LibrarySourceState> states,Dictionary<string,FileInfo> files)
        {
            if(states!=null&&states.TryGetValue(mount.Id,out var cached))return cached;
            var state=MountState(mount,force,files);if(states!=null)states[mount.Id]=state;return state;
        }
        private LibrarySourceState State(LibraryCatalogue data,LibraryAnimation clip,bool force=false,Dictionary<string,LibrarySourceState> states=null,
            Dictionary<string,LibraryModel> models=null,Dictionary<string,LibraryMount> mounts=null,Dictionary<string,FileInfo> files=null)
        {
            var motion=mounts==null?data.Mounts.Single(m=>m.Id==clip.MountId):mounts[clip.MountId];
            var state=CachedMountState(motion,force,states,files);
            if(state!=LibrarySourceState.Available)return state;
            if(motion.Provider!=BrawlLibrarySource.Provider || clip.Channel!="CHR0 skeletal" || (clip.NativeVersion!=4&&clip.NativeVersion!=5) || clip.FrameCount<1||clip.FrameCount>4096)
                return LibrarySourceState.UnsupportedChannelVersion;
            if(clip.ModelId==null)return LibrarySourceState.NeedsModelAssociation;
            var model=models==null?data.Models.Single(m=>m.Id==clip.ModelId):models[clip.ModelId];
            return CachedMountState(mounts==null?data.Mounts.Single(m=>m.Id==model.MountId):mounts[model.MountId],force,states,files);
        }
        public List<LibrarySearchItem> Search(string query,int offset,out int total,int pageSize=100)
            =>SearchRows(new LibraryQuery{Text=query},offset,out total,pageSize,false);
        // Generic additive overloads retain unambiguous legacy Search(null)/Browse(null).
        public List<LibrarySearchItem> Search<TQuery>(TQuery query,int offset,out int total,int pageSize=100) where TQuery:LibraryQuery
            =>SearchRows(query??new LibraryQuery(),offset,out total,pageSize,false);
        public List<LibrarySearchItem> Browse(string query)
            =>SearchRows(new LibraryQuery{Text=query},0,out _,MaximumClips,true);
        public List<LibrarySearchItem> Browse<TQuery>(TQuery query) where TQuery:LibraryQuery
            =>SearchRows(query??new LibraryQuery(),0,out _,MaximumClips,true);
        private List<LibrarySearchItem> SearchRows(LibraryQuery query,int offset,out int total,int pageSize,bool outline)
        {
            query=query.Snapshot();
            lock(cacheSync)
            {
            if(offset<0 || pageSize<1 || pageSize>(outline?MaximumClips:200) || (query.Text?.Length??0)>512)throw new ArgumentException("Invalid library search range.");
            var data=SearchSnapshot();
            if(!ReferenceEquals(indexedCatalogue,data))
            {
                indexedModels=data.Models.ToDictionary(m=>m.Id);indexedMounts=data.Mounts.ToDictionary(m=>m.Id);
                indexedClips=data.Clips.OrderBy(c=>c.Name,StringComparer.OrdinalIgnoreCase).ThenBy(c=>c.ResourceKey,StringComparer.Ordinal).Select(c=>{
                    indexedModels.TryGetValue(c.ModelId??"",out var model);var motion=indexedMounts[c.MountId];
                    return new IndexedClip{Clip=c,Model=model,Motion=motion,GameText=motion.Game+" "+motion.GameVersion,ModelText=model?.Character+" "+model?.Name+" "+c.SourceCharacter,Text=c.Name+" "+model?.Name+" "+model?.Character+" "+c.SourceCharacter+" "+motion.Game+" "+motion.GameVersion+" "+c.Channel+" "+c.Tags};
                }).ToArray();indexedCatalogue=data;
            }
            var words=LibraryQuery.Words(query.Text);var gameWords=LibraryQuery.Words(query.GameBuildText);var modelWords=LibraryQuery.Words(query.CharacterModelText);
            var favoriteIds=query.FavoritesOnly?new HashSet<string>(favorites.Value.Ids,StringComparer.Ordinal):null;
            var states=new Dictionary<string,LibrarySourceState>();
            var files=new Dictionary<string,FileInfo>(StringComparer.OrdinalIgnoreCase);
            var found=indexedClips.Where(c=>LibraryQuery.WordsMatch(c.Text,words) &&
                LibraryQuery.Exact(c.Motion.Game,query.Game) && LibraryQuery.Exact(c.Motion.GameVersion,query.Build) &&
                LibraryQuery.Exact(c.Model?.Character??c.Clip.SourceCharacter,query.Character) && LibraryQuery.Exact(c.Model?.Id,query.ModelId) &&
                LibraryQuery.Exact(c.Clip.Channel,query.Channel) &&
                LibraryQuery.WordsMatch(c.GameText,gameWords) &&
                LibraryQuery.WordsMatch(c.ModelText,modelWords) &&
                (favoriteIds==null||favoriteIds.Contains(c.Clip.Id))).ToArray();
            if(query.Availability.HasValue) found=found.Where(c=>State(data,c.Clip,false,states,indexedModels,indexedMounts,files)==query.Availability.Value).ToArray();
            total=found.Length;
            return found.Skip(offset).Take(pageSize).Select(c=>new LibrarySearchItem{Clip=c.Clip,Model=c.Model,Motion=c.Motion,State=State(data,c.Clip,false,states,indexedModels,indexedMounts,files)}).ToList();
            }
        }
        // Costume enumeration uses the immutable metadata generation only. In
        // particular it never hashes a source or decodes a model while browsing.
        public LibraryCostumeOption[] GetCostumes(string clipId)
        {
            if(!Id(clipId))throw new InvalidDataException("Choose an exact registered library clip identity before listing costumes.");
            lock(cacheSync)
            {
                var data=SearchSnapshot();
                if(!ReferenceEquals(costumeCatalogue,data)){costumeCatalogue=data;costumeChoices.Clear();}
                if(!costumeChoices.TryGetValue(clipId,out var choices))
                {
                    var clip=data.Clips.SingleOrDefault(c=>c.Id==clipId)??throw new InvalidDataException("This immutable library clip is no longer registered.");
                    choices=BrawlLibraryCostumes.Choices(data,clip);
                    if(costumeChoices.Count>=64)costumeChoices.Clear();
                    costumeChoices.Add(clipId,choices);
                }
                return (LibraryCostumeOption[])choices.Clone();
            }
        }

        public LibrarySourceLease Open(string clipId)=>Open(clipId,null);

        public LibrarySourceLease Open(string clipId,string modelId)
        {
            Interlocked.Increment(ref DiagnosticOpens);
            var data=Read();var clip=data.Clips.SingleOrDefault(c=>c.Id==clipId)??throw new InvalidDataException("This immutable library clip is no longer registered.");
            var state=State(data,clip,true);
            if(state!=LibrarySourceState.Available)throw new InvalidOperationException("Library source: "+state+". Repair the exact source or review its model association first.");
            var model=data.Models.Single(m=>m.Id==clip.ModelId);
            if(modelId!=null&&modelId!=model.Id)
            {
                var choice=BrawlLibraryCostumes.Choices(data,clip).SingleOrDefault(c=>c.ModelId==modelId);
                if(choice==null||!choice.Available)throw new InvalidDataException(choice?.UnavailableReason??"This exact model is not a verified Brawl costume for the selected source animation/build.");
                model=data.Models.Single(m=>m.Id==modelId);
            }
            var a=data.Mounts.Single(m=>m.Id==model.MountId);var b=data.Mounts.Single(m=>m.Id==clip.MountId);
            if(model.Id!=clip.ModelId)
            {
                var modelState=MountState(a,true);
                if(modelState!=LibrarySourceState.Available)throw new InvalidOperationException("Library costume source: "+modelState+". Repair the exact source before previewing or transferring it.");
            }
            var originSource=a.GameSourceId==null?null:data.GameLocations.Single(l=>l.Id==a.GameSourceId);
            var lease=new LibrarySourceLease{ModelPath=a.LocalPath};
            try
            {
                foreach(var origin in new[]{a,b}.Where(m=>m.OriginPath!=null).GroupBy(m=>m.OriginPath,StringComparer.OrdinalIgnoreCase).Select(g=>g.First()))
                {
                    NoLinks(origin.OriginPath);var held=new FileStream(origin.OriginPath,FileMode.Open,FileAccess.Read,FileShare.Read);lease.OriginLocks.Add(held);
                    if(held.Length!=origin.OriginBytes)throw new InvalidDataException("The original disc changed before native decoding.");
                }
                lease.ModelLock=LockSource(a.LocalPath);lease.MotionLock=LockSource(b.LocalPath);
                if(FighterProject.HashFile(a.LocalPath)!=a.ContainerSha256 || FighterProject.HashFile(b.LocalPath)!=b.ContainerSha256)throw new InvalidDataException("Source changed before decoding.");
                lease.ModelRoot=NodeFactory.FromFile(null,a.LocalPath);lease.MotionRoot=NodeFactory.FromFile(null,b.LocalPath);
                if(lease.ModelRoot==null||lease.MotionRoot==null)throw new InvalidDataException("The indexed native source cannot be decoded.");
                lease.Model=BrawlLibrarySource.Resolve(lease.ModelRoot,model.ResourceKey) as MDL0Node;
                lease.Animation=BrawlLibrarySource.Resolve(lease.MotionRoot,clip.ResourceKey) as CHR0Node;
                if(lease.Model==null||lease.Animation==null||BrawlLibrarySource.ResourceHash(lease.Model)!=model.ResourceSha256||BrawlLibrarySource.ResourceHash(lease.Animation)!=clip.ResourceSha256||
                    BrawlRetargeter.SkeletonFingerprint(lease.Model)!=model.SkeletonSha256||lease.Model.Name!=model.Name||lease.Animation.Name!=clip.Name||
                    lease.Model.Version!=model.NativeVersion||lease.Animation.Version!=clip.NativeVersion||lease.Animation.FrameCount!=clip.FrameCount||lease.Animation.Loop!=clip.Loop)
                    throw new InvalidDataException("Native source model, skeleton, resource or timing identity differs from the approved association.");
                lease.Reference=new LibraryClipReference{ClipId=clip.Id,Revision=clip.Revision,ModelId=model.Id,Game=a.Game,GameVersion=a.GameVersion,Platform=a.Platform,
                    BuildEvidence=a.BuildEvidence,Adapter=a.Provider,ModelMountId=a.Id,MotionMountId=b.Id,ModelKey=model.ResourceKey,MotionKey=clip.ResourceKey,
                    ModelContainerSha256=a.ContainerSha256,MotionContainerSha256=b.ContainerSha256,ModelResourceSha256=model.ResourceSha256,MotionResourceSha256=clip.ResourceSha256,
                    SourceRig=model.SkeletonSha256,RestInterpretation=model.RestInterpretation,AssociationEvidence=model.Id==clip.ModelId?clip.AssociationEvidence:BrawlLibraryCostumes.AssociationEvidence(clip,model),
                    NativeVersion=clip.NativeVersion,FrameCount=clip.FrameCount,Loop=clip.Loop,TicksPerSecond=clip.TicksPerSecond,TimebaseEvidence=clip.TimebaseEvidence,Channel=clip.Channel,
                    GameSourceId=a.GameSourceId,GameSourceSha256=a.OriginSha256??(a.GameSourceId==null?null:data.GameLocations.Single(l=>l.Id==a.GameSourceId).ContainerSha256),
                    ModelVirtualPath=a.VirtualPath,MotionVirtualPath=b.VirtualPath,UnboundSourceTracks=clip.UnboundSourceTracks,
                    SourceProvider=originSource?.Provider,ExtractorSha256=originSource?.ToolSha256,
                    ModelPartition=a.OriginPartition??(BrawlGameSource.IsDiscProvider(originSource?.Provider)?"DATA":null),
                    MotionPartition=b.OriginPartition??(BrawlGameSource.IsDiscProvider(originSource?.Provider)?"DATA":null)};
                return lease;
            }
            catch{lease.Dispose();throw;}
        }
    }
}
