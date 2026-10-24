using BrawlCrate.Studio.Retargeting;
using BrawlLib.SSBB.ResourceNodes;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;

namespace BrawlCrate.Studio
{
    public sealed class LibraryImportPlan
    {
        internal LibraryRegistration Request;
        internal LibraryMount ModelMount, MotionMount;
        public LibraryModel Model { get; internal set; }
        public List<LibraryAnimation> Clips { get; internal set; }
    }

    public sealed class LibrarySourceLease : IDisposable
    {
        internal ResourceNode ModelRoot, MotionRoot;
        internal FileStream ModelLock, MotionLock;
        internal readonly List<FileStream> OriginLocks=new List<FileStream>();
        public MDL0Node Model { get; internal set; }
        public CHR0Node Animation { get; internal set; }
        public LibraryClipReference Reference { get; internal set; }
        public string ModelPath { get; internal set; }
        public void Dispose()
        {
            MotionRoot?.Dispose(); ModelRoot?.Dispose();
            MotionLock?.Dispose(); ModelLock?.Dispose();
            foreach(var origin in OriginLocks)origin.Dispose();OriginLocks.Clear();
            MotionRoot=ModelRoot=null; MotionLock=ModelLock=null;
        }
    }

    public static class BrawlLibrarySource
    {
        public const string Provider = "brawl-file-v1";
        public const int MaximumNodes = 100000;
        public static IEnumerable<Tuple<ResourceNode,string>> Resources(ResourceNode root,CancellationToken cancellation=default)
        {
            var pending=new Stack<Tuple<ResourceNode,string,int>>();pending.Push(Tuple.Create(root,"$",0));int count=0;
            while(pending.Count>0)
            {
                cancellation.ThrowIfCancellationRequested();var item=pending.Pop();
                if(++count>MaximumNodes || item.Item3>32)throw new InvalidDataException("Source resource hierarchy exceeds the library limit.");
                yield return Tuple.Create(item.Item1,item.Item2);
                switch(item.Item1.ResourceFileType)
                {
                    case ResourceType.ARC:case ResourceType.MRG:case ResourceType.U8:case ResourceType.U8Folder:
                    case ResourceType.BRES:case ResourceType.BRESGroup:
                        var children=item.Item1.Children;
                        for(int i=children.Count-1;i>=0;i--)pending.Push(Tuple.Create(children[i],item.Item2+"/"+i,item.Item3+1));
                        break;
                }
            }
        }
        public static ResourceNode Resolve(ResourceNode root,string key)
        {
            if(string.IsNullOrEmpty(key) || key.Length>512)throw new InvalidDataException("Invalid native resource identity.");
            string[] parts=key.Split('/');if(parts[0]!="$" || parts.Length>33)throw new InvalidDataException("Invalid native resource identity.");
            var node=root;
            foreach(string part in parts.Skip(1))
            {
                if(!int.TryParse(part,out int index) || part!=index.ToString(System.Globalization.CultureInfo.InvariantCulture) || index<0 || index>=node.Children.Count)
                    throw new InvalidDataException("The indexed native resource no longer exists.");
                node=node.Children[index];
            }
            return node;
        }
        public static unsafe string ResourceHash(ResourceNode node)
        {
            var data=node.WorkingUncompressed;
            if(data.Length<=0 || data.Length>FighterProject.MaximumAssetBytes)throw new InvalidDataException("Invalid native resource size.");
            using(var stream=new UnmanagedMemoryStream((byte*)data.Address,data.Length))
            using(var sha=SHA256.Create())return Hex(sha.ComputeHash(stream));
        }
        internal static string Hex(byte[] data)=>BitConverter.ToString(data).Replace("-","").ToLowerInvariant();

        public static List<LibraryModel> InspectModels(string path,CancellationToken cancellation=default)
        {
            using(var input=AnimationLibrary.LockSource(path))
            using(var root=NodeFactory.FromFile(null,path))
            {
                if(root==null)throw new InvalidDataException("No supported Brawl resource was found.");
                // MDL0's geometry/bone caches are lazy. Describe first so the
                // native skeleton walk populates them before checking geometry.
                return Resources(root,cancellation).Where(p=>p.Item1 is MDL0Node).Select(p=>new{Model=(MDL0Node)p.Item1,Record=DescribeModel((MDL0Node)p.Item1,p.Item2)})
                    .Where(p=>p.Model.Objects.Length>0).Select(p=>p.Record).ToList();
            }
        }
        internal static LibraryModel DescribeModel(MDL0Node model,string key)=>new LibraryModel{
            Id=Guid.NewGuid().ToString("N"),Name=model.Name,Character=model.Name,Variant=model.Name,ResourceKey=key,
            ResourcePath=model.TreePath,ResourceSha256=ResourceHash(model),SkeletonSha256=BrawlRetargeter.SkeletonFingerprint(model),NativeVersion=model.Version,
            HasGeometry=model.Objects.Length>0,Shadow=model.Name.IndexOf("shadow",StringComparison.OrdinalIgnoreCase)>=0||model.Name.EndsWith("shd",StringComparison.OrdinalIgnoreCase)};

        public static LibraryImportPlan Prepare(LibraryRegistration request,CancellationToken cancellation=default,Action<int,string> progress=null)
        {
            if(request==null || string.IsNullOrWhiteSpace(request.Character) || string.IsNullOrWhiteSpace(request.GameVersion) ||
                !(new[]{"Brawl","Project M","Project+"}).Contains(request.Game))throw new InvalidDataException("Choose a source character, Brawl-family game and explicit build/version (or mark it unknown).");
            string modelPath=AnimationLibrary.OrdinaryPath(request.ModelPath),motionPath=AnimationLibrary.OrdinaryPath(request.MotionPath);
            if(!new[]{".pac",".pcs",".brres",".mdl0"}.Contains(Path.GetExtension(modelPath).ToLowerInvariant()) ||
                !new[]{".pac",".pcs",".brres",".chr0"}.Contains(Path.GetExtension(motionPath).ToLowerInvariant()))
                throw new NotSupportedException("L0 indexes Brawl model PAC/PCS/BRRES/MDL0 and motion PAC/PCS/BRRES/CHR0 only. Other source providers are pending.");
            using(var modelLock=AnimationLibrary.LockSource(modelPath))using(var motionLock=AnimationLibrary.LockSource(motionPath))
            using(var modelRoot=NodeFactory.FromFile(null,modelPath))using(var motionRoot=NodeFactory.FromFile(null,motionPath))
            {
                cancellation.ThrowIfCancellationRequested();progress?.Invoke(5,"Reading original model and motion metadata...");
                if(modelRoot==null || motionRoot==null)throw new InvalidDataException("A source package could not be decoded.");
                var models=Resources(modelRoot,cancellation).Where(p=>p.Item1 is MDL0Node).ToArray();
                var selected=models.Where(p=>p.Item2==request.ModelKey).ToArray();
                if(selected.Length!=1)throw new InvalidDataException("Choose the exact original native model; ambiguous model ownership cannot be guessed.");
                var source=(MDL0Node)selected[0].Item1;
                var described=DescribeModel(source,request.ModelKey);described.Character=request.Character;
                var modelMount=Mount(request,modelPath);var motionMount=Mount(request,motionPath);described.MountId=modelMount.Id;
                var bones=source.AllBones.ToArray();var names=new HashSet<string>(bones.Select(b=>b.Name),StringComparer.Ordinal);
                if(names.Count!=bones.Length)throw new InvalidDataException("The source model has ambiguous duplicate bone identities.");
                var semantic=BrawlRetargeter.Characterize(source);
                var bodyNames=new HashSet<string>(semantic.Roles.Where(p=>p.Key=="pelvis" || p.Key=="spine" || p.Key=="chest" || p.Key=="neck" ||
                    p.Key.EndsWith(".upperLeg",StringComparison.Ordinal) || p.Key.EndsWith(".upperArm",StringComparison.Ordinal)).Select(p=>bones[p.Value].Name));
                var native=Resources(motionRoot,cancellation).Where(p=>p.Item1 is NW4RAnimationNode).ToArray();
                if(native.Length==0 || native.Length>AnimationLibrary.MaximumClips)throw new InvalidDataException("The motion package contains no animations or exceeds the bounded index limit.");
                var clips=new List<LibraryAnimation>();
                for(int i=0;i<native.Length;i++)
                {
                    cancellation.ThrowIfCancellationRequested();var node=(NW4RAnimationNode)native[i].Item1;
                    bool skeletal=node is CHR0Node;
                    bool supported=skeletal && (node.Version==4 || node.Version==5) && node.FrameCount>0 && node.FrameCount<=4096;
                    var unbound=node.Children.Where(n=>!names.Contains(n.Name)).Select(n=>n.Name).ToArray();
                    bool binding=supported && node.Children.Count>0 && unbound.Length<=node.Children.Count/10 && node.Children.Count(n=>bodyNames.Contains(n.Name))>=2;
                    bool approved=request.ApproveAssociation && binding;
                    clips.Add(new LibraryAnimation{Id=Guid.NewGuid().ToString("N"),MountId=motionMount.Id,ModelId=approved?described.Id:null,
                        Name=node.Name,SourceCharacter=request.Character,ResourceKey=native[i].Item2,ResourcePath=node.TreePath,ResourceSha256=ResourceHash(node),
                        Channel=skeletal?"CHR0 skeletal":node.GetType().Name,NativeVersion=node.Version,FrameCount=node.FrameCount,Loop=node.Loop,
                        UnboundSourceTracks=skeletal&&unbound.Length>0?string.Join(", ",unbound):null,
                        AssociationEvidence=approved?"User-reviewed model/motion pair, pinned container/resource hashes, exact source skeleton and native body-track binding validated":null,
                        AssociationIssue=!supported?"Unsupported channel/version":!request.ApproveAssociation?"Pair not approved":!binding?"Body ownership not established; choose the authentic source model":null,
                        Tags=request.Character+" "+request.Game});
                    progress?.Invoke(10+(i+1)*85/native.Length,"Indexing "+(i+1)+" / "+native.Length+" native animations...");
                }
                return new LibraryImportPlan{Request=request,ModelMount=modelMount,MotionMount=motionMount,Model=described,Clips=clips};
            }
        }
        private static LibraryMount Mount(LibraryRegistration request,string path)=>new LibraryMount{Id=Guid.NewGuid().ToString("N"),LocalPath=path,
            Game=request.Game,GameVersion=request.GameVersion,BuildEvidence=request.BuildEvidence,ContainerSha256=FighterProject.HashFile(path),Bytes=new FileInfo(path).Length};
    }
}
