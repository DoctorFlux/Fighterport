using BrawlCrate.Studio;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Web.Script.Serialization;
using System.Xml.Linq;

internal static partial class Program
{
    private static int DiscCases(string[] args)
    {
        string image=Path.GetFullPath(Arg(args,"--disc-image")),tool=Path.GetFullPath(Arg(args,"--dolphin-tool"));
        string output=Path.GetFullPath(Arg(args,"--output"));
        if(Directory.Exists(output)&&Directory.EnumerateFileSystemEntries(output).Any())throw new IOException("Choose empty output.");
        Directory.CreateDirectory(output);string before=Hash(image);var clock=Stopwatch.StartNew();
        var request=new LibraryGameRequest{Game="Brawl",GameVersion="User supplied Brawl disc; build not inferred",Path=image,DolphinToolPath=tool,ApproveBuild=true};
        var characters=BrawlGameSource.BrowseCharacters(request);
        Check(characters.Length>2&&characters.Contains("mario")&&characters.Contains("snake"),"Disc roster is discovered from the actual native filesystem",new{Characters=characters});
        request.Characters=(Arg(args,"--characters")??"mario,snake").Split(',');
        var library=new AnimationLibrary(Path.Combine(output,"SharedLibrary"));
        var plan=BrawlGameSource.Prepare(request,Path.Combine(library.DirectoryPath,"NativeCache"),default,(p,message)=>Console.WriteLine(message));
        int added=library.RegisterGame(plan);
        Check(added==plan.Clips.Count&&added>600,"Every animation resource in the selected character packages is registered atomically",new{Clips=added,Models=plan.Models.Count,Files=plan.Source.NativeFiles,Issues=plan.Issues});
        Check(plan.Mounts.All(m=>request.Characters.Contains(m.VirtualPath.Split('/')[1]))&&plan.Mounts.All(m=>m.OriginSha256==before),"Only selected characters are cached and every resource retains the image hash and virtual path");
        if(Arg(args,"--compare-index")!=null)
        {
            var previous=new AnimationLibrary(Arg(args,"--compare-index")).Read();
            Check(plan.Mounts.All(m=>previous.Mounts.Any(p=>p.VirtualPath==m.VirtualPath&&p.ContainerSha256==m.ContainerSha256)),"Different disc containers extract identical native source files");
        }
        foreach(string character in request.Characters)
        {
            var result=library.Search(character+" Wait1",0,out int total,200).Where(i=>i.State==LibrarySourceState.Available&&i.Clip.Name=="Wait1").ToArray();
            Check(result.Length>=1,character+": verified original body motion is available",new{Matches=total});
            using(var source=library.Open(result[0].Clip.Id))
                Check(source.Reference.GameSourceSha256==before&&source.Reference.ModelVirtualPath.StartsWith("fighter/"+character+"/")&&source.Reference.MotionVirtualPath.StartsWith("fighter/"+character+"/")&&
                    source.Reference.SourceProvider=="dolphin-brawl-disc-v1"&&source.Reference.ExtractorSha256==Hash(tool)&&source.Reference.ModelPartition=="DATA"&&source.Reference.MotionPartition=="DATA",character+": native clip resolves its exact original model, extractor, partition and disc origin");
            if(character=="captain"&&Arg(args,"--snake-root")!=null)BakeProject(library,result[0].Clip.Id,Arg(args,"--snake-root"),"Snake",Path.Combine(output,"Captain-to-Snake.fpstudio"),true);
        }
        string index=library.IndexPath;var xml=XDocument.Load(index);string sourceId=plan.Source.Id;
        foreach(var mount in xml.Root.Element("Mounts").Elements())mount.Element("OriginPath").Value=Path.Combine(output,"offline-image.wbfs");
        xml.Root.Element("GameLocations").Elements().Single(l=>l.Element("Id").Value==sourceId).Element("Path").Value=Path.Combine(output,"offline-image.wbfs");xml.Save(index);
        Check(new AnimationLibrary(library.DirectoryPath).Search("",0,out _).All(i=>i.State==LibrarySourceState.Missing),"Offline image retains searchable metadata and blocks conversion");
        library.RepairGameSource(sourceId,image);
        Check(new AnimationLibrary(library.DirectoryPath).Search("mario Wait1",0,out _).Any(i=>i.State==LibrarySourceState.Available),"Exact image repair preserves approved source associations");
        using(var cancel=new CancellationTokenSource())
        {cancel.Cancel();Refuses(()=>BrawlGameSource.Prepare(request,Path.Combine(output,"CancelledCache"),cancel.Token),"Cancelled disc indexing publishes no source set");}
        Check(Hash(image)==before,"Original disc image remains byte-for-byte unchanged");
        File.WriteAllText(Path.Combine(output,"disc-results.json"),new JavaScriptSerializer{MaxJsonLength=16000000}.Serialize(new{Passed=true,Checks,Characters=characters,Selected=request.Characters,ImageSha256=before,ToolSha256=Hash(tool),Milliseconds=clock.ElapsedMilliseconds}));
        Console.WriteLine("PASS: "+Checks.Count+" disc-source checks.");return 0;
    }
}
