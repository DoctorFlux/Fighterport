using System;
using System.IO;
using System.IO.Compression;
using System.Threading.Tasks;

namespace BrawlCrate.Studio
{
    internal sealed class NativeWorkspaceInfo
    {
        public string Id {get;set;}public string Name {get;set;}public string Path {get;set;}public bool Dirty {get;set;}public bool RequiresUpgradeCopy {get;set;}
        public string ActiveTarget {get;set;}public string ActiveScene {get;set;}
        public string[] ClipIds {get;set;}
        public NativeWorkspaceTarget[] Targets {get;set;}public NativeWorkspaceScene[] Scenes {get;set;}
    }
    internal sealed class NativeWorkspaceTarget
    {
        public string Id {get;set;}public string Name {get;set;}public string Mode {get;set;}public NativeWorkspaceBuild Build {get;set;}
        public override string ToString()=>Name;
    }
    internal sealed class NativeWorkspaceBuild{public string Game {get;set;}public string Platform {get;set;}public string Build {get;set;}}
    internal sealed class NativeWorkspaceScene
    {
        public string Id {get;set;}public string Name {get;set;}public string Target {get;set;}public string DerivedFrom {get;set;}public bool NeedsSharedReview {get;set;}
        public override string ToString()=>Name+(NeedsSharedReview?" · shared revision changed":"");
    }
    internal sealed class NativeWorkspaceSession
    {
        internal static readonly NativeWorkspaceSession Shared=new NativeWorkspaceSession();
        private readonly AnimationControlClient _client=new AnimationControlClient();
        internal readonly string Stage=System.IO.Path.Combine(System.IO.Path.GetTempPath(),"FighterPort","NextWorkspaces",Guid.NewGuid().ToString("N"));
        internal NativeWorkspaceInfo Info;
        internal async Task<ControlReply> Send(object request)
        {
            var reply=await _client.Send(request);if(reply.Workspace!=null)Info=reply.Workspace;return reply;
        }
        internal string NewFolder(){string path=System.IO.Path.Combine(Stage,Guid.NewGuid().ToString("N"));Directory.CreateDirectory(path);return path;}
        internal void Dispose()=>_client.Dispose();
        internal static string ExtractPackage(string package,string folder)
        {
            Directory.CreateDirectory(folder);string prefix=System.IO.Path.GetFullPath(folder)+System.IO.Path.DirectorySeparatorChar;long total=0;
            using(var archive=ZipFile.OpenRead(package))
            {
                if(archive.Entries.Count>1024)throw new InvalidDataException("Too many native editor snapshot files.");
                foreach(var entry in archive.Entries)
                {
                    if(entry.FullName.EndsWith("/",StringComparison.Ordinal))continue;
                    if(entry.Length<0||entry.Length>FighterProject.MaximumAssetBytes||(total+=entry.Length)>4L*1024*1024*1024)throw new InvalidDataException("The native editor snapshot exceeds its file limit.");
                    if(entry.FullName!="project.fpstudio"&&!entry.FullName.StartsWith("project.fpstudio.assets/",StringComparison.Ordinal))throw new InvalidDataException("Unexpected native editor snapshot entry.");
                    string destination=System.IO.Path.GetFullPath(System.IO.Path.Combine(folder,entry.FullName.Replace('/',System.IO.Path.DirectorySeparatorChar)));
                    if(!destination.StartsWith(prefix,StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("A snapshot path escaped its folder.");
                    Directory.CreateDirectory(System.IO.Path.GetDirectoryName(destination));
                    using(var input=entry.Open())using(var output=new FileStream(destination,FileMode.CreateNew,FileAccess.Write,FileShare.None))input.CopyTo(output);
                }
            }
            string manifest=System.IO.Path.Combine(folder,"project.fpstudio");if(!File.Exists(manifest))throw new InvalidDataException("Missing native editor document.");return manifest;
        }
    }
}
