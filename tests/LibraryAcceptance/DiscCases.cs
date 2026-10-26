using BrawlCrate.Studio;
using BrawlCrate.Studio.Retargeting;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Web.Script.Serialization;
using System.Xml.Linq;

internal static partial class Program
{
    private static AnimationLibrary CostumeCatalogueCopy(AnimationLibrary original,string output,string scenario,Action<XDocument> change)
    {
        string folder=Path.Combine(output,"costume-metadata-"+scenario);Directory.CreateDirectory(folder);
        var data=XDocument.Load(original.IndexPath);change(data);data.Save(Path.Combine(folder,"catalogue.xml"));
        return new AnimationLibrary(folder);
    }
    private static XElement CostumeElement(XDocument data,string group,string id)=>data.Root.Element(group).Elements().Single(e=>e.Element("Id").Value==id);
    private static string DifferentCostumeHash(string value)=>new string(value==new string('0',64)?'1':'0',64);
    private static void CostumeRefuses(Action action,string expected,string name)
    {
        string error=null;
        try{action();}catch(Exception e) when(e is InvalidDataException||e is InvalidOperationException||e is IOException){error=e.Message;}
        Check(error!=null&&error.Contains(expected),name,new{Expected=expected,Error=error});
    }
    private static void DiscCostumes(AnimationLibrary library,string clipId,string character,string output)
    {
        var data=library.Read();var clip=data.Clips.Single(c=>c.Id==clipId);string catalogueHash=Hash(library.IndexPath);
        int hashes=FighterProject.DiagnosticFileHashes,opens=AnimationLibrary.DiagnosticOpens,bakes=BrawlRetargeter.DiagnosticBakes;
        var choices=library.GetCostumes(clipId);
        for(int repeat=0;repeat<5;repeat++)library.GetCostumes(clipId);
        Check(FighterProject.DiagnosticFileHashes==hashes&&AnimationLibrary.DiagnosticOpens==opens&&BrawlRetargeter.DiagnosticBakes==bakes,
            character+": costume enumeration and repeated metadata queries perform no source hashing, native opening or retarget baking");
        var canonical=choices.Single(c=>c.IsCanonical);var alternate=choices.FirstOrDefault(c=>!c.IsCanonical&&c.Available&&
            !string.Equals(Path.GetFileNameWithoutExtension(c.Variant),Path.GetFileNameWithoutExtension(canonical.Variant),StringComparison.OrdinalIgnoreCase));
        Check(canonical.ModelId==clip.ModelId&&canonical.Available&&alternate!=null,
            character+": actual disc inventory exposes the exact original association and a distinct compatible costume rather than its other container format",
            new{Choices=choices.Select(c=>new{c.ModelId,c.Variant,c.Available,c.UnavailableReason}).ToArray()});
        var model=data.Models.Single(m=>m.Id==alternate.ModelId);var mount=data.Mounts.Single(m=>m.Id==model.MountId);
        var motion=data.Mounts.Single(m=>m.Id==clip.MountId);string modelFileHash=Hash(mount.LocalPath),motionFileHash=Hash(motion.LocalPath);
        using(var source=library.Open(clipId))using(var costume=library.Open(clipId,alternate.ModelId))
        {
            Check(source.Reference.ModelId==clip.ModelId&&source.Reference.ModelResourceSha256==data.Models.Single(m=>m.Id==clip.ModelId).ResourceSha256,
                character+": unchanged Open(clipId) still resolves the canonical exact model");
            Check(costume.Reference.ClipId==clip.Id&&costume.Reference.ModelId==model.Id&&costume.Reference.ModelMountId==mount.Id&&
                costume.Reference.ModelContainerSha256==modelFileHash&&costume.Reference.ModelResourceSha256==model.ResourceSha256&&
                costume.Reference.SourceRig==model.SkeletonSha256&&costume.Reference.SourceRig==source.Reference.SourceRig&&
                BrawlLibrarySource.ResourceHash(costume.Model)==model.ResourceSha256&&BrawlRetargeter.SkeletonFingerprint(costume.Model)==model.SkeletonSha256&&
                costume.Reference.MotionResourceSha256==clip.ResourceSha256&&costume.Reference.MotionContainerSha256==motionFileHash&&
                costume.Reference.GameSourceId==source.Reference.GameSourceId&&costume.Reference.ModelVirtualPath==mount.VirtualPath,
                character+": costume lease pins the selected actual model, container, skeleton, native motion and disc provenance");
            string beforeModel=BrawlLibrarySource.ResourceHash(costume.Model),beforeClip=BrawlLibrarySource.ResourceHash(costume.Animation);
            bool modelDirty=costume.Model.IsDirty,clipDirty=costume.Animation.IsDirty;
            costume.Model.ApplyCHR(costume.Animation,1);costume.Model.ApplyCHR(costume.Animation,costume.Animation.FrameCount);
            Check(BrawlLibrarySource.ResourceHash(costume.Animation)==BrawlLibrarySource.ResourceHash(source.Animation)&&
                BrawlLibrarySource.ResourceHash(costume.Animation)==beforeClip&&BrawlLibrarySource.ResourceHash(costume.Model)==beforeModel&&
                costume.Model.IsDirty==modelDirty&&costume.Animation.IsDirty==clipDirty,
                character+": original native costume playback retains every authored curve byte, model byte and dirty state");
        }
        string scenario=character+"-";
        var changedBind=CostumeCatalogueCopy(library,output,scenario+"bind",xml=>{
            var item=CostumeElement(xml,"Models",model.Id);item.Element("SkeletonSha256").Value=DifferentCostumeHash(item.Element("SkeletonSha256").Value);
            // A missing alternative path proves the bind rejection is reached
            // before attempting to decode that model or silently substituting.
            CostumeElement(xml,"Mounts",mount.Id).Element("LocalPath").Value=Path.Combine(output,"unavailable-costume.pac");
        });
        var unavailable=changedBind.GetCostumes(clipId).Single(c=>c.ModelId==model.Id);
        Check(!unavailable.Available&&unavailable.UnavailableReason.Contains("hierarchy or bind"),
            character+": a different recorded native bind is explicitly unavailable pending review");
        CostumeRefuses(()=>{using(var ignored=changedBind.Open(clipId,model.Id)){ }},"hierarchy or bind",
            character+": different-bind selection rejects before decoding and never falls back to the original model");

        var canonicalMount=data.Mounts.Single(m=>m.Id==data.Models.Single(original=>original.Id==clip.ModelId).MountId);
        string canonicalFile=Path.GetFileNameWithoutExtension(canonicalMount.VirtualPath),family=canonicalFile.Substring(0,canonicalFile.Length-2);
        var localized=CostumeCatalogueCopy(library,output,scenario+"localized",xml=>
            CostumeElement(xml,"Mounts",mount.Id).Element("VirtualPath").Value="fighter/"+character+"/"+family+"01_en.pac");
        var localizedPcs=CostumeCatalogueCopy(library,output,scenario+"localized-pcs",xml=>
            CostumeElement(xml,"Mounts",mount.Id).Element("VirtualPath").Value="fighter/"+character+"/"+family+"01_EN.pcs");
        hashes=FighterProject.DiagnosticFileHashes;opens=AnimationLibrary.DiagnosticOpens;
        Check(new[]{localized,localizedPcs}.All(c=>c.GetCostumes(clipId).Any(option=>option.ModelId==model.Id&&option.Available))&&
            FighterProject.DiagnosticFileHashes==hashes&&AnimationLibrary.DiagnosticOpens==opens,
            character+": optional two-letter locale suffixes inventory compatible PAC/PCS costumes without hashing or opening sources");
        var foreignLocalized=CostumeCatalogueCopy(library,output,scenario+"localized-foreign-family",xml=>
            CostumeElement(xml,"Mounts",mount.Id).Element("VirtualPath").Value="fighter/"+character+"/FitUnrelated01_en.pac");
        var localizedBind=CostumeCatalogueCopy(localized,output,scenario+"localized-bind",xml=>
            CostumeElement(xml,"Models",model.Id).Element("SkeletonSha256").Value=DifferentCostumeHash(model.SkeletonSha256));
        Check(!foreignLocalized.GetCostumes(clipId).Any(option=>option.ModelId==model.Id)&&
            localizedBind.GetCostumes(clipId).Any(option=>option.ModelId==model.Id&&!option.Available&&option.UnavailableReason.Contains("hierarchy or bind")),
            character+": localized costume filenames do not relax exact native family or hierarchy/bind compatibility");

        var foreignSource=CostumeCatalogueCopy(library,output,scenario+"foreign-source",xml=>{
            string other=Guid.NewGuid().ToString("N");var location=new XElement(CostumeElement(xml,"GameLocations",mount.GameSourceId));
            location.Element("Id").Value=other;xml.Root.Element("GameLocations").Add(location);
            CostumeElement(xml,"Mounts",mount.Id).Element("GameSourceId").Value=other;
        });
        var foreignCharacter=CostumeCatalogueCopy(library,output,scenario+"foreign-character",xml=>
            CostumeElement(xml,"Mounts",mount.Id).Element("VirtualPath").Value="fighter/unrelated/"+Path.GetFileName(mount.VirtualPath));
        var foreignFamily=CostumeCatalogueCopy(library,output,scenario+"foreign-family",xml=>
            CostumeElement(xml,"Mounts",mount.Id).Element("VirtualPath").Value="fighter/"+character+"/FitUnrelated01.pac");
        Check(new[]{foreignSource,foreignCharacter,foreignFamily}.All(c=>!c.GetCostumes(clipId).Any(option=>option.ModelId==model.Id)),
            character+": matching labels/binds cannot admit another indexed game source, native character folder or costume package family");
        CostumeRefuses(()=>{using(var ignored=foreignSource.Open(clipId,model.Id)){ }},"not a verified Brawl costume",
            character+": explicit loading also rejects a foreign source model instead of accepting its matching label");

        var badResource=CostumeCatalogueCopy(library,output,scenario+"resource",xml=>{
            var item=CostumeElement(xml,"Models",model.Id);item.Element("ResourceSha256").Value=DifferentCostumeHash(item.Element("ResourceSha256").Value);
        });
        CostumeRefuses(()=>{using(var ignored=badResource.Open(clipId,model.Id)){ }},"Native source model, skeleton, resource or timing identity differs",
            character+": decoded selected costume resource must match its own exact resource hash");

        string damaged=Path.Combine(output,"damaged-costume-"+character+Path.GetExtension(mount.LocalPath));File.Copy(mount.LocalPath,damaged);
        using(var stream=new FileStream(damaged,FileMode.Open,FileAccess.ReadWrite,FileShare.None))
        {stream.Position=stream.Length-1;int value=stream.ReadByte();stream.Position=stream.Length-1;stream.WriteByte((byte)(value^1));}
        var changedFile=CostumeCatalogueCopy(library,output,scenario+"changed-file",xml=>{
            var item=CostumeElement(xml,"Mounts",mount.Id);item.Element("LocalPath").Value=damaged;
            item.Element("NativeWriteTicks").Value=File.GetLastWriteTimeUtc(damaged).Ticks.ToString(System.Globalization.CultureInfo.InvariantCulture);
        });
        CostumeRefuses(()=>{using(var ignored=changedFile.Open(clipId,model.Id)){ }},"Library costume source: SourceChanged",
            character+": changed output-owned costume bytes fail exact selected-container verification");
        Check(Hash(library.IndexPath)==catalogueHash&&Hash(mount.LocalPath)==modelFileHash&&Hash(motion.LocalPath)==motionFileHash&&BrawlRetargeter.DiagnosticBakes==bakes,
            character+": costume browsing, playback and all rejected selections preserve the original catalogue, original native files and retargeter state");
    }

    private static void ExtractedFolderCostumes(AnimationLibrary discLibrary,LibraryGamePlan discPlan,string[] characters,string output)
    {
        var first=discPlan.Mounts.First();string relative=first.VirtualPath.Replace('/',Path.DirectorySeparatorChar);
        if(!first.LocalPath.EndsWith(relative,StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("Native extraction lost its recorded relative path.");
        string root=first.LocalPath.Substring(0,first.LocalPath.Length-relative.Length).TrimEnd(Path.DirectorySeparatorChar);
        Check(discPlan.Mounts.All(m=>string.Equals(m.LocalPath,Path.Combine(root,m.VirtualPath.Replace('/',Path.DirectorySeparatorChar)),StringComparison.OrdinalIgnoreCase)),
            "Extracted-folder fixture is the actual common native filesystem produced by the selected disc inventory");
        var request=new LibraryGameRequest{Game="Brawl",GameVersion=discPlan.Source.GameVersion,Path=root,ApproveBuild=true,Characters=characters};
        var roster=BrawlGameSource.BrowseCharacters(request);
        Check(characters.All(roster.Contains)&&request.DolphinToolPath==null,
            "Extracted Brawl folders browse every selected character without any disc reader/tool path");
        var library=new AnimationLibrary(Path.Combine(output,"ExtractedFolderLibrary"));
        var plan=BrawlGameSource.Prepare(request,Path.Combine(library.DirectoryPath,"UnusedCache"));
        Check(plan.Source.Provider=="brawl-folder-v1"&&plan.Source.IsDirectory&&plan.Source.ToolPath==null&&plan.Source.ToolSha256==null&&
            plan.Mounts.All(m=>m.GameSourceId==plan.Source.Id&&m.OriginPath==null)&&library.RegisterGame(plan)==plan.Clips.Count,
            "Extracted-folder indexing registers a separate exact folder source without borrowing disc/tool provenance");
        Check(plan.Models.Count==discPlan.Models.Count&&plan.Clips.Count==discPlan.Clips.Count&&
            plan.Models.All(m=>discPlan.Models.Any(d=>d.Character==m.Character&&d.ResourceKey==m.ResourceKey&&d.ResourceSha256==m.ResourceSha256&&d.SkeletonSha256==m.SkeletonSha256))&&
            plan.Clips.All(c=>discPlan.Clips.Any(d=>d.SourceCharacter==c.SourceCharacter&&d.ResourceKey==c.ResourceKey&&d.ResourceSha256==c.ResourceSha256)),
            "Disc and extracted-folder routes independently recover the same native models, skeletons and animation resource hashes");
        foreach(string character in characters)
        {
            var clip=library.Search(character+" Wait1",0,out _,200).First(i=>i.State==LibrarySourceState.Available&&i.Clip.SourceCharacter==character&&i.Clip.Name=="Wait1").Clip;
            int hashes=FighterProject.DiagnosticFileHashes,opens=AnimationLibrary.DiagnosticOpens,bakes=BrawlRetargeter.DiagnosticBakes;
            var choices=library.GetCostumes(clip.Id);var canonical=choices.Single(c=>c.IsCanonical);
            var alternate=choices.FirstOrDefault(c=>!c.IsCanonical&&c.Available&&
                !string.Equals(Path.GetFileNameWithoutExtension(c.Variant),Path.GetFileNameWithoutExtension(canonical.Variant),StringComparison.OrdinalIgnoreCase));
            Check(choices.Count(c=>c.Available)>1&&alternate!=null&&FighterProject.DiagnosticFileHashes==hashes&&AnimationLibrary.DiagnosticOpens==opens&&BrawlRetargeter.DiagnosticBakes==bakes,
                character+": extracted-folder costumes expose a distinct compatible costume using metadata only",
                new{Original=canonical.Variant,Selected=alternate?.Variant});
            var model=plan.Models.Single(m=>m.Id==alternate.ModelId);var mount=plan.Mounts.Single(m=>m.Id==model.MountId);
            var discClip=discPlan.Clips.First(c=>c.SourceCharacter==character&&c.ResourceKey==clip.ResourceKey&&c.ResourceSha256==clip.ResourceSha256&&c.ModelId!=null);
            var discModel=discPlan.Models.Single(m=>m.ResourceKey==model.ResourceKey&&m.ResourceSha256==model.ResourceSha256&&
                discPlan.Mounts.Single(a=>a.Id==m.MountId).VirtualPath==mount.VirtualPath);
            using(var source=library.Open(clip.Id,model.Id))using(var disc=discLibrary.Open(discClip.Id,discModel.Id))
            {
                Check(source.Reference.SourceProvider=="brawl-folder-v1"&&source.Reference.GameSourceId==plan.Source.Id&&source.Reference.GameSourceId!=disc.Reference.GameSourceId&&
                    source.Reference.ModelId==model.Id&&source.Reference.ModelMountId==mount.Id&&source.Reference.ModelVirtualPath==mount.VirtualPath&&
                    source.Reference.ModelResourceSha256==model.ResourceSha256&&source.Reference.MotionResourceSha256==clip.ResourceSha256&&source.Reference.ExtractorSha256==null,
                    character+": selected folder costume resolves its own exact identities and folder provenance without substituting the disc source");
                string modelHash=BrawlLibrarySource.ResourceHash(source.Model),clipHash=BrawlLibrarySource.ResourceHash(source.Animation);
                bool modelDirty=source.Model.IsDirty,clipDirty=source.Animation.IsDirty,posesEqual=true;
                foreach(int frame in new[]{1,source.Animation.FrameCount})
                {
                    source.Model.ApplyCHR(source.Animation,frame);disc.Model.ApplyCHR(disc.Animation,frame);
                    var a=source.Model.AllBones.ToArray();var b=disc.Model.AllBones.ToArray();
                    posesEqual&=a.Length==b.Length&&a.Select((bone,index)=>bone.Name==b[index].Name&&
                        Enumerable.Range(0,9).All(c=>bone.FrameState[c]==b[index].FrameState[c])&&Enumerable.Range(0,16).All(c=>bone.Matrix[c]==b[index].Matrix[c])).All(v=>v);
                }
                Check(posesEqual&&BrawlLibrarySource.ResourceHash(source.Model)==modelHash&&BrawlLibrarySource.ResourceHash(source.Animation)==clipHash&&
                    source.Model.IsDirty==modelDirty&&source.Animation.IsDirty==clipDirty,
                    character+": selected extracted-folder costume evaluates identical first/last native local and world poses while original bytes and dirty state remain unchanged");
            }
        }
    }

    private static void SplitDiscGuard(string output)
    {
        string folder=Path.Combine(output,"split-wbfs-guard");Directory.CreateDirectory(folder);
        string image=Path.Combine(folder,"split.wbfs");File.WriteAllBytes(image,new byte[]{0x57,0x42,0x46,0x53});
        File.WriteAllBytes(Path.Combine(folder,"split.wbf1"),new byte[]{1});
        int hashes=FighterProject.DiagnosticFileHashes,opens=AnimationLibrary.DiagnosticOpens,bakes=BrawlRetargeter.DiagnosticBakes;
        var request=new LibraryGameRequest{Game="Brawl",Path=image,DolphinToolPath=Path.Combine(folder,"DolphinTool.exe")};
        CostumeRefuses(()=>BrawlGameSource.BrowseCharacters(request),"Split WBFS files need a reader that verifies every part",
            "Output-owned split WBFS companion rejects before a missing external reader can be started");
        Check(FighterProject.DiagnosticFileHashes==hashes&&AnimationLibrary.DiagnosticOpens==opens&&BrawlRetargeter.DiagnosticBakes==bakes,
            "Split WBFS rejection performs no file hashing, native source opening or retarget baking");
    }

    private static int DiscCases(string[] args)
    {
        string image=Path.GetFullPath(Arg(args,"--disc-image"));
        string output=Path.GetFullPath(Arg(args,"--output"));
        if(Directory.Exists(output)&&Directory.EnumerateFileSystemEntries(output).Any())throw new IOException("Choose empty output.");
        Directory.CreateDirectory(output);string before=Hash(image);var clock=Stopwatch.StartNew();
        var request=new LibraryGameRequest{Game="Brawl",GameVersion="User supplied Brawl disc; build not inferred",Path=image,DolphinToolPath=Path.Combine(output,"not-installed","DolphinTool.exe"),ApproveBuild=true};
        var characters=BrawlGameSource.BrowseCharacters(request);
        Check(characters.Length>2&&characters.Contains("mario")&&characters.Contains("snake"),"Disc roster is discovered from the actual native filesystem",new{Characters=characters});
        request.Characters=args.Contains("--all-characters")?null:(Arg(args,"--characters")??"mario,snake").Split(',');
        string[] inspectedCharacters=request.Characters??characters;
        if(Arg(args,"--legacy-index")!=null)
        {
            string directory=Path.Combine(output,"LegacyLibrary");Directory.CreateDirectory(directory);
            var previous=new AnimationLibrary(Arg(args,"--legacy-index"));var legacyXml=XDocument.Load(previous.IndexPath);
            foreach(var location in legacyXml.Root.Element("GameLocations").Elements().Where(l=>l.Element("Provider")?.Value=="dolphin-brawl-disc-v1"))
                location.Element("ToolPath").Value=Path.Combine(output,"not-installed","DolphinTool.exe");
            legacyXml.Save(Path.Combine(directory,"catalogue.xml"));var legacy=new AnimationLibrary(directory);
            var old=legacy.Browse("mario Wait1").First(i=>i.State==LibrarySourceState.Available&&i.Clip.Channel=="CHR0 skeletal");
            using(var source=legacy.Open(old.Clip.Id))Check(source.Reference.SourceProvider=="dolphin-brawl-disc-v1"&&source.Animation.FrameCount>0,
                "Previously indexed disc sources reopen without the historical reader executable, preserving exact old provenance");
        }
        var library=new AnimationLibrary(Path.Combine(output,"SharedLibrary"));
        var plan=BrawlGameSource.Prepare(request,Path.Combine(library.DirectoryPath,"NativeCache"),default,(p,message)=>Console.WriteLine(message));
        int added=library.RegisterGame(plan);
        Check(plan.Source.Provider==BrawlDiscImage.Provider&&plan.Source.ToolPath==null&&plan.Source.ToolSha256==null,"Disc indexing uses the built-in reader even with a nonexistent legacy executable request path");
        Check(added==plan.Clips.Count&&added>600,"Every animation resource in the selected character packages is registered atomically",new{Clips=added,Models=plan.Models.Count,Files=plan.Source.NativeFiles,Issues=plan.Issues});
        Check(plan.Mounts.All(m=>request.Characters==null||request.Characters.Contains(m.VirtualPath.Split('/')[1]))&&plan.Mounts.All(m=>m.OriginSha256==before),"Only selected characters are cached and every resource retains the image hash and virtual path");
        string registeredHash=Hash(library.IndexPath);var registered=library.Read();
        Check(library.RegisterGame(plan)==0&&Hash(library.IndexPath)==registeredHash&&
            library.Read().Models.Select(m=>m.Id).SequenceEqual(registered.Models.Select(m=>m.Id))&&
            library.Read().Clips.Select(c=>c.Id).SequenceEqual(registered.Clips.Select(c=>c.Id)),
            "Repeated publication of the verified complete native inventory preserves every known model/clip ID and exact catalogue bytes");
        if(request.Characters!=null)ExtractedFolderCostumes(library,plan,request.Characters,output);
        SplitDiscGuard(output);
        if(Arg(args,"--compare-index")!=null)
        {
            var previous=new AnimationLibrary(Arg(args,"--compare-index")).Read();
            Check(previous.Mounts.All(p=>plan.Mounts.Any(m=>p.VirtualPath==m.VirtualPath&&p.ContainerSha256==m.ContainerSha256)),"Every independently known native source file is extracted byte-identically from this disc container");
        }
        foreach(string character in inspectedCharacters)
        {
            var result=library.Search(character+" Wait1",0,out int total,200).Where(i=>i.State==LibrarySourceState.Available&&i.Clip.SourceCharacter==character&&i.Clip.Name=="Wait1").ToArray();
            if(result.Length==0){Check(args.Contains("--all-characters"),character+": no canonical Wait1; resources remain explicitly indexed without a substitute model");continue;}
            Check(result.Length>=1,character+": verified original body motion is available",new{Matches=total});
            using(var source=library.Open(result[0].Clip.Id))
                Check(source.Reference.GameSourceSha256==before&&source.Reference.ModelVirtualPath.StartsWith("fighter/"+character+"/")&&source.Reference.MotionVirtualPath.StartsWith("fighter/"+character+"/")&&
                    source.Reference.SourceProvider==BrawlDiscImage.Provider&&source.Reference.ExtractorSha256==null&&source.Reference.ModelPartition=="DATA"&&source.Reference.MotionPartition=="DATA",character+": native clip resolves its exact original model, built-in reader, partition and disc origin");
            if(new[]{"mario","snake","captain","wario","kirby"}.Contains(character))DiscCostumes(library,result[0].Clip.Id,character,output);
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
        File.WriteAllText(Path.Combine(output,"disc-results.json"),new JavaScriptSerializer{MaxJsonLength=16000000}.Serialize(new{Passed=true,Checks,Characters=characters,Selected=request.Characters,ImageSha256=before,ReaderProvider=BrawlDiscImage.Provider,ExternalReaderRequired=false,Milliseconds=clock.ElapsedMilliseconds}));
        var summary=library.Read();
        File.WriteAllText(Path.Combine(output,"association-summary.json"),new JavaScriptSerializer{MaxJsonLength=64000000}.Serialize(new{Models=summary.Models.Count,Clips=summary.Clips.Count,Mounts=summary.Mounts.Count,
            Groups=summary.Clips.GroupBy(c=>c.SourceCharacter).Select(g=>new{Character=g.Key,All=g.Count(),Skeletal=g.Count(c=>c.Channel=="CHR0 skeletal"),Linked=g.Count(c=>c.ModelId!=null),Unlinked=g.Where(c=>c.Channel=="CHR0 skeletal"&&c.ModelId==null).Select(c=>new{c.Name,c.ResourcePath,c.AssociationIssue})})}));
        Console.WriteLine("PASS: "+Checks.Count+" disc-source checks.");return 0;
    }
}
