// Real native fixtures are supplied at runtime, never bundled or modified.
using BrawlCrate.Studio;
using BrawlCrate.Studio.Retargeting;
using BrawlLib.SSBB.ResourceNodes;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Web.Script.Serialization;
using System.Xml.Linq;

internal static partial class Program
{
    private static readonly List<object> Checks=new List<object>();
    private static void Check(bool ok,string name,object evidence=null)
    {if(!ok)throw new InvalidDataException(name+" "+new JavaScriptSerializer().Serialize(evidence));Checks.Add(new{name,evidence});Console.WriteLine("PASS "+name);}
    private static void Refuses(Action action,string name)
    {bool refused=false;try{action();}catch(Exception e) when(e is InvalidDataException||e is InvalidOperationException||e is IOException||e is OperationCanceledException){refused=true;}Check(refused,name);}
    private static string Arg(string[] args,string key)=>args.SkipWhile(a=>a!=key).Skip(1).FirstOrDefault();
    private static string Hash(string path)=>FighterProject.HashFile(path);
    private static Dictionary<string,string> Hashes(params string[] roots)=>roots.SelectMany(r=>Directory.GetFiles(r,"*",SearchOption.AllDirectories)).ToDictionary(p=>p,Hash,StringComparer.OrdinalIgnoreCase);
    private static IEnumerable<ResourceNode> Walk(ResourceNode root)=>BrawlLibrarySource.Resources(root).Select(p=>p.Item1);
    private static MDL0Node Body(ResourceNode root,string character)=>Walk(root).OfType<MDL0Node>().Single(m=>m.Name=="Fit"+character+"00");
    private static string RootPath(string root,string character,string suffix)=>Path.Combine(root,"Fit"+character+suffix+".pac");
    private static LibraryRegistration Request(string root,string character,bool approved=true)
    {
        string path=RootPath(root,character,"00");var model=BrawlLibrarySource.InspectModels(path).Single(m=>m.Name=="Fit"+character+"00");
        return new LibraryRegistration{ModelPath=path,ModelKey=model.ResourceKey,MotionPath=RootPath(root,character,"MotionEtc"),Character=character,
            GameVersion="RSBE01 revision 1",BuildEvidence="User-reported Dolphin extraction; RSBE01 revision 1 observed in supplied WBFS header. Package-to-disc membership not independently verified.",ApproveAssociation=approved};
    }
    [STAThread] private static int Main(string[] args)
    {
        CultureInfo.CurrentCulture=CultureInfo.InvariantCulture;
        try
        {
            if(args.Length==2&&args[0]=="--reopen")return Reopen(args[1]);
            if(args.Contains("--disc-image"))return DiscCases(args);
            string mario=Arg(args,"--mario-root"),snake=Arg(args,"--snake-root"),output=Path.GetFullPath(Arg(args,"--output")??throw new ArgumentException("Supply a new --output directory."));
            if(Directory.Exists(output)&&Directory.EnumerateFileSystemEntries(output).Any())throw new IOException("Output must be empty.");
            Directory.CreateDirectory(output);var before=Hashes(mario,snake);
            var library=new AnimationLibrary(Path.Combine(output,"SharedLibrary"));
            var marioRequest=Request(mario,"Mario");var snakeRequest=Request(snake,"Snake");
            var clock=Stopwatch.StartNew();var plan=BrawlLibrarySource.Prepare(marioRequest);
            int independent;using(var motion=NodeFactory.FromFile(null,marioRequest.MotionPath))independent=Walk(motion).OfType<NW4RAnimationNode>().Count();
            Check(plan.Clips.Count==independent,"Whole motion pack inventories every native animation",new{Clips=independent,Milliseconds=clock.ElapsedMilliseconds});
            Check(library.Register(plan)==independent,"Atomic whole-pack registration");
            Refuses(()=>library.ProtectOutputPath(marioRequest.ModelPath),"Library originals are protected even when not registered in the target fighter");
            Refuses(()=>library.ProtectOutputPath(marioRequest.MotionPath),"Donor motion packages cannot be native export destinations");
            Check(library.CanUndo,"The first source registration is undoable");
            string firstIndexHash=Hash(library.IndexPath);Check(library.Register(BrawlLibrarySource.Prepare(marioRequest))==0&&Hash(library.IndexPath)==firstIndexHash,"Repeated registration keeps immutable IDs and bytes");
            var marioWait=library.Read().Clips.Single(c=>c.Name=="Wait1"&&c.ModelId!=null);string marioId=marioWait.Id;
            Check(library.Search("Mario Wait1",0,out int total).Any(c=>c.Clip.Id==marioId)&&total>=1,"Metadata search resolves character and clip");
            var otherWriter=new AnimationLibrary(library.DirectoryPath);var snakePlan=BrawlLibrarySource.Prepare(snakeRequest);library.Register(snakePlan);
            Check(otherWriter.Read().Models.Count==2,"A second library client sees the committed shared catalogue");
            Check(library.CanUndo,"Registration has one reversible catalogue transaction");library.UndoLastUpdate();
            Check(library.Read().Models.Count==1&&library.Read().Clips.Any(c=>c.Id==marioId)&&!library.CanUndo,"Undo removes the whole new source set without changing retained IDs");
            library.Register(BrawlLibrarySource.Prepare(snakeRequest));
            string snakeId=library.Search("Snake Wait1",0,out total).Single(c=>c.Clip.Name=="Wait1"&&c.State==LibrarySourceState.Available).Clip.Id;
            var complete=library.Browse("");
            Check(complete.Count==library.Read().Clips.Count&&complete.Count(i=>i.Model?.Character=="Mario")>250&&complete.Count(i=>i.Model?.Character=="Snake")>300,
                "Complete grouped browsing retains every fighter's animations beyond a global search page");
            var snakeBrowse=library.Browse("Snake");
            Check(snakeBrowse.Count>300&&snakeBrowse.All(i=>(i.Model?.Character??i.Clip.SourceCharacter)=="Snake"),"Character browsing returns the complete filtered metadata set without native decoding");
            string preCancel=Hash(library.IndexPath);using(var cancel=new CancellationTokenSource())
            {cancel.Cancel();Refuses(()=>library.Register(BrawlLibrarySource.Prepare(marioRequest),cancel.Token),"Cancelled commit refuses publication");Refuses(()=>BrawlLibrarySource.Prepare(marioRequest,cancel.Token),"Cancelled inventory stops without entries");}
            Check(preCancel==Hash(library.IndexPath),"Cancellation preserves the complete previous catalogue");
            using(var cancel=new CancellationTokenSource())Refuses(()=>BrawlLibrarySource.Prepare(marioRequest,cancel.Token,(p,message)=>{if(p>=30)cancel.Cancel();}),"Cancellation during partial native inventory publishes no entries");
            Check(preCancel==Hash(library.IndexPath),"Partial inventory cancellation preserves the committed generation");
            Check(library.Read().Clips.Any(c=>c.ModelId==null),"Article and unsupported channels retain explicit unresolved status");
            using(var original=library.Open(marioId))
            {
                var rig=BrawlRetargeter.Capture(original.Model);var a=BrawlRetargeter.Sample(rig,original.Animation,0);var b=BrawlRetargeter.Sample(rig,original.Animation,original.Animation.FrameCount/2);
                var emptyObject=new MDL0ObjectNode{Name="Undecoded empty object"};original.Model.PolygonList.Add(emptyObject);original.Model.PolygonList.Add(null);
                try{var withEmpty=BrawlRetargeter.Capture(original.Model);Check(withEmpty.Rig.Surfaces.All(p=>p.Value.Points.Length==rig.Rig.Surfaces[p.Key].Points.Length),"Empty mesh objects and null entries do not crash or change valid contact surfaces");}
                finally{original.Model.PolygonList.Remove(emptyObject);original.Model.PolygonList.Remove(null);emptyObject.Dispose();}
                var part=original.Model.PolygonList.OfType<MDL0ObjectNode>().First(p=>p.FaceCount>0);var manager=part._manager;
                try{part._manager=null;Refuses(()=>BrawlRetargeter.Capture(original.Model),"Unavailable nonempty mesh reports a contact-rig error instead of a null-reference crash");}
                finally{part._manager=manager;}
                Check((a.Point("pelvis")-b.Point("pelvis")).Length()>0.001f,"Original source model and its motion produce changing native poses");
                Check(original.Reference.ModelContainerSha256==Hash(marioRequest.ModelPath)&&original.Reference.MotionContainerSha256==Hash(marioRequest.MotionPath)&&original.Reference.GameVersion=="RSBE01 revision 1","Lease resolves pinned original model, skeleton, build and both container hashes");
            }
            BakeProject(library,marioId,snake,"Snake",Path.Combine(output,"Snake-A.fpstudio"),true);
            BakeProject(library,marioId,snake,"Snake",Path.Combine(output,"Snake-B.fpstudio"),false);
            BakeProject(library,snakeId,mario,"Mario",Path.Combine(output,"Mario-A.fpstudio"),true);
            BakeProject(library,marioId,mario,"Mario",Path.Combine(output,"Mario-Self.fpstudio"),false);
            string otherProject=Hash(Path.Combine(output,"Snake-B.fpstudio"));string firstBake;
            using(var a=FighterProject.Open(Path.Combine(output,"Snake-A.fpstudio")))
            using(var b=FighterProject.Open(Path.Combine(output,"Snake-B.fpstudio")))
            {
                Check(a.Animations.Single().Id!=b.Animations.Single().Id&&a.Animations.Single().Retarget.Library.ClipId==b.Animations.Single().Retarget.Library.ClipId,"Two independent fighter projects share one immutable source clip");
                var asset=a.Animations.Single();firstBake=asset.Retarget.Library.InitialBakeSha256;
                using(var clip=(CHR0Node)NodeFactory.FromFile(null,asset.WorkingPath))
                {var track=clip.Children.OfType<CHR0EntryNode>().First();track.SetKeyframe(3,0,track.GetFrameValue(3,0)+0.5f);clip.Merge();clip.ExportChecked(asset.WorkingPath);}
                a.Save(a.ManifestPath);
                Check(asset.ContentSha256!=firstBake&&b.Animations.Single().ContentSha256==firstBake&&Hash(b.Animations.Single().WorkingPath)==firstBake,"Editing one target revision leaves the other target bake and immutable source intact");
            }
            Check(Hash(Path.Combine(output,"Snake-B.fpstudio"))==otherProject,"Editing one fighter does not rewrite the second fighter descriptor");
            var child=Process.Start(new ProcessStartInfo(typeof(Program).Assembly.Location,"--reopen \""+output+"\""){UseShellExecute=false,CreateNoWindow=true});
            if(!child.WaitForExit(120000)){child.Kill();throw new TimeoutException("Restart validation timed out.");}
            Check(child.ExitCode==0,"A fresh process restores the shared library and independent edited projects");
            FailureCases(marioRequest,output);
            SearchScale(library,output);
            ManageSources(library,output);
            var after=Hashes(mario,snake);Check(before.Count==after.Count&&before.All(p=>after.TryGetValue(p.Key,out var hash)&&hash==p.Value),"All original fixture files remain byte-for-byte unchanged",new{Files=before.Count});
            var data=library.Read();var states=library.Search("",0,out total,200).GroupBy(i=>i.State).ToDictionary(g=>g.Key.ToString(),g=>g.Count());
            var report=new{Passed=true,Checks,Models=data.Models.Count,Clips=data.Clips.Count,FirstPageStates=states,SourceFiles=before.Count,ExecutableSha256=Hash(typeof(FighterProject).Assembly.Location)};
            File.WriteAllText(Path.Combine(output,"library-results.json"),new JavaScriptSerializer{MaxJsonLength=16000000}.Serialize(report),new UTF8Encoding(false));
            Console.WriteLine("PASS: "+Checks.Count+" source-bound library checks.");return 0;
        }
        catch(Exception e){Console.Error.WriteLine(e);return 1;}
    }
    private static void BakeProject(AnimationLibrary library,string id,string targetRoot,string character,string projectPath,bool pac)
    {
        using(var source=library.Open(id))using(var project=FighterProject.Create(character+" Library"))
        {
            project.Model=project.StageAsset(RootPath(targetRoot,character,"00"),"model");
            using(var root=NodeFactory.FromFile(null,project.Model.WorkingPath))
            {
                var model=Body(root,character);string rest=BrawlRetargeter.SkeletonFingerprint(model);
                var target=BrawlRetargeter.Capture(model);var original=BrawlRetargeter.Capture(source.Model);
                using(var result=BrawlRetargeter.Bake(original,target,source.Animation))
                {
                    var asset=project.CreateLibraryRevision(source,result);project.Animations.Add(asset);project.ModelNodePath=model.TreePath;project.SelectedAnimation=asset.Id+"|"+result.Animation.TreePath;project.Frame=1;
                    using(var saved=(CHR0Node)NodeFactory.FromFile(null,asset.WorkingPath))
                    {
                        Check(saved.FrameCount==source.Animation.FrameCount&&saved.Loop==source.Animation.Loop&&saved.Children.All(n=>target.Bones.Any(b=>b.Name==n.Name)),character+": native bake retains timing and uses target bones");
                        float error=0,rootError=0;
                        var motion=result.Plan.Mapping.FirstOrDefault(m=>m.Role=="motion");
                        for(float time=0;time<saved.FrameCount;time+=0.25f)
                        {
                            float t=Math.Min(time,saved.FrameCount-1);var expected=BrawlRetargeter.Sample(target,result.Animation,t);var actual=BrawlRetargeter.Sample(target,saved,t);
                            foreach(string role in target.Rig.Roles.Keys){var delta=actual.Point(role)-expected.Point(role);error=Math.Max(error,delta.Length());CheckFinite(actual.Point(role).X,actual.Point(role).Y,actual.Point(role).Z);}
                            if(motion!=null){var from=source.Animation.Children.OfType<CHR0EntryNode>().SingleOrDefault(n=>n.Name==motion.Source);var to=saved.Children.OfType<CHR0EntryNode>().SingleOrDefault(n=>n.Name==motion.Target);if(from!=null&&to!=null)rootError=Math.Max(rootError,Math.Abs(from.GetFrameValue(8,t)-to.GetFrameValue(8,t)));}
                        }
                        // CHR0's native compressed channels quantize keys; retain
                        // the existing 0.06-unit native round-trip bound.
                        Check(error<0.06f&&rootError<0.0001f,character+": fresh native quarter-frame playback matches bake and preserves TransN Z",new{MaximumJointError=error,MaximumRootZError=rootError});
                        if(pac)
                        {
                            string output=projectPath+".pac";BrawlRetargeter.ExportMotionPac(RootPath(targetRoot,character,"MotionEtc"),output,model,saved);
                            using(var exported=NodeFactory.FromFile(null,output))using(var template=NodeFactory.FromFile(null,RootPath(targetRoot,character,"MotionEtc")))
                            {
                                var a=Walk(exported).OfType<CHR0Node>().ToArray();var b=Walk(template).OfType<CHR0Node>().ToArray();
                                Check(a.Length==b.Length&&a.Single(c=>c.Name==saved.Name).FrameCount==saved.FrameCount,character+": target motion PAC exports and reopens with all native slots");
                            }
                            asset.Retarget.Library.LastExportSha256=Hash(output);asset.Retarget.Library.LastExportFormat="PAC";
                        }
                    }
                    Check(rest==BrawlRetargeter.SkeletonFingerprint(model)&&asset.Retarget.Library.TargetRig==rest,character+": target body proportions and bind skeleton remain unchanged");
                    project.Save(projectPath);
                }
            }
        }
    }
    private static void CheckFinite(params float[] values){if(values.Any(v=>float.IsNaN(v)||float.IsInfinity(v)))throw new InvalidDataException("Non-finite native pose");}
    private static int Reopen(string output)
    {
        var library=new AnimationLibrary(Path.Combine(output,"SharedLibrary"));
        var a=FighterProject.Open(Path.Combine(output,"Snake-A.fpstudio"));var b=FighterProject.Open(Path.Combine(output,"Snake-B.fpstudio"));
        using(a)using(b)
        {
            var left=a.Animations.Single();var right=b.Animations.Single();
            Check(left.Retarget.Library.ClipId==right.Retarget.Library.ClipId&&left.ContentSha256!=right.ContentSha256,"Restart retains shared source and separate target edits");
            using(var source=library.Open(left.Retarget.Library.ClipId))Check(source.Reference.SourceRig==left.Retarget.SourceRig,"Restart resolves the saved exact source without another model choice");
            using(var native=NodeFactory.FromFile(null,left.WorkingPath))Check(native is CHR0Node,"Restart reopens edited native target");
        }
        return 0;
    }
    private static void FailureCases(LibraryRegistration request,string output)
    {
        string root=Path.Combine(output,"FailureCases");Directory.CreateDirectory(root);
        string model=Path.Combine(root,"model.pac"),motion=Path.Combine(root,"motion.pac");File.Copy(request.ModelPath,model);File.Copy(request.MotionPath,motion);
        var local=new LibraryRegistration{ModelPath=model,MotionPath=motion,ModelKey=request.ModelKey,Character=request.Character,GameVersion=request.GameVersion};
        var library=new AnimationLibrary(Path.Combine(root,"Index"));library.Register(BrawlLibrarySource.Prepare(local));
        string bodyKey=BrawlLibrarySource.Prepare(request).Clips.Single(c=>c.Name=="Wait1"&&c.ModelId!=null).ResourceKey;
        var wait=library.Search("Wait1",0,out _).Single(c=>c.Clip.ResourceKey==bodyKey);
        Check(wait.State==LibrarySourceState.NeedsModelAssociation,"Matching bone names alone never approve model ownership");Refuses(()=>{using(var lease=library.Open(wait.Clip.Id)){}},"Unreviewed source cannot preview or retarget");
        local.ApproveAssociation=true;library.Register(BrawlLibrarySource.Prepare(local));wait=library.Search("Wait1",0,out _).Single(c=>c.State==LibrarySourceState.Available&&c.Clip.Name=="Wait1");
        var catalogue=library.Read();var mount=catalogue.Mounts.Single(m=>m.Id==wait.Clip.MountId);string before=Hash(library.IndexPath);
        string relocated=Path.Combine(root,"relocated.pac");File.Move(motion,relocated);
        Check(library.Search("Wait1",0,out _).All(c=>c.State==LibrarySourceState.Missing),"Missing motion mount is visible and blocks reuse");
        Refuses(()=>{using(var lease=library.Open(wait.Clip.Id)){}},"Missing exact source refuses conversion");
        Refuses(()=>library.Repair(mount.Id,model),"Repair rejects a different native file");Check(before==Hash(library.IndexPath),"Failed repair preserves catalogue bytes and approval");
        library.Repair(mount.Id,relocated);using(var lease=library.Open(wait.Clip.Id))Check(lease.Reference.ClipId==wait.Clip.Id,"Exact relocated source repairs without changing clip identity");
        using(var stream=new FileStream(relocated,FileMode.Open,FileAccess.Write,FileShare.None)){stream.Position=stream.Length-1;stream.WriteByte(0x7d);}
        Check(library.Search("Wait1",0,out _).All(c=>c.State==LibrarySourceState.SourceChanged),"Changed bytes are labelled source changed");Refuses(()=>{using(var lease=library.Open(wait.Clip.Id)){}},"Changed original bytes cannot silently reuse the old association");
        File.Copy(request.MotionPath,relocated,true);
        library.Repair(mount.Id,relocated); // Reverify restored bytes outside the browsing path.
        string modelMoved=Path.Combine(root,"relocated-model.pac");File.Move(model,modelMoved);
        Check(library.Search("Wait1",0,out _).Where(c=>c.Clip.ModelId!=null).All(c=>c.State==LibrarySourceState.Missing),"Missing bound model blocks otherwise available animations");
        var modelMount=catalogue.Models.Single(m=>m.Id==wait.Model.Id).MountId;library.Repair(modelMount,modelMoved);
        using(var lease=library.Open(wait.Clip.Id))Check(lease.Reference.ModelId==wait.Model.Id,"Repairing original model retains exact association identity");
        string validIndex=File.ReadAllText(library.IndexPath);var metadata=XDocument.Parse(validIndex);
        var selected=metadata.Root.Element("Clips").Elements().Single(c=>c.Element("Id").Value==wait.Clip.Id);
        selected.Element("NativeVersion").Value="99";metadata.Save(library.IndexPath);
        Check(library.Search("Wait1",0,out _).Single(c=>c.Clip.Id==wait.Clip.Id).State==LibrarySourceState.UnsupportedChannelVersion,"Unknown native channel version is reported unsupported");
        Refuses(()=>{using(var lease=library.Open(wait.Clip.Id)){}},"Unsupported native version cannot preview or convert");
        File.WriteAllText(library.IndexPath,validIndex);metadata=XDocument.Parse(validIndex);
        metadata.Root.Element("Clips").Elements().Single(c=>c.Element("Id").Value==wait.Clip.Id).Element("FrameCount").Value="52";
        metadata.Save(library.IndexPath);Refuses(()=>{using(var lease=library.Open(wait.Clip.Id)){}},"Native timing must match its pinned library interpretation");
        File.WriteAllText(library.IndexPath,validIndex);
        string malformed=Path.Combine(root,"Malformed");Directory.CreateDirectory(malformed);string index=Path.Combine(malformed,"catalogue.xml");File.WriteAllText(index,"<!DOCTYPE x [<!ENTITY x SYSTEM 'file:///never-read'>]><x>&x;</x>");
        string badHash=Hash(index);Refuses(()=>new AnimationLibrary(malformed).Read(),"Malformed/DTD catalogue refuses external entities");Check(Hash(index)==badHash,"Malformed catalogue is preserved for recovery");
        Refuses(()=>BrawlLibrarySource.Resolve(new ARCNode(),"$/../0"),"Native resource resolver rejects noncanonical branch identities");
        DuplicateBranches(request,root);
    }
    private static void DuplicateBranches(LibraryRegistration request,string output)
    {
        string file=Path.Combine(output,"duplicates.pac");
        using(var original=NodeFactory.FromFile(null,request.MotionPath))using(var root=new ARCNode{Name="Duplicates"})
        {
            string bodyKey=BrawlLibrarySource.Prepare(request).Clips.Single(c=>c.Name=="Wait1"&&c.ModelId!=null).ResourceKey;
            var wait=(CHR0Node)BrawlLibrarySource.Resolve(original,bodyKey);string copy=Path.Combine(output,"copy.chr0");wait.ExportChecked(copy);
            for(int i=0;i<2;i++){var branch=new BRRESNode{Name="RepeatedBranch"};root.AddChild(branch);var clip=NodeFactory.FromFile(null,copy);branch.GetOrCreateFolder<CHR0Node>().AddChild(clip);}
            root.ExportChecked(file);
        }
        var local=new LibraryRegistration{ModelPath=request.ModelPath,ModelKey=request.ModelKey,MotionPath=file,Character=request.Character,GameVersion=request.GameVersion,ApproveAssociation=true};
        var library=new AnimationLibrary(Path.Combine(output,"DuplicateIndex"));var plan=BrawlLibrarySource.Prepare(local);library.Register(plan);
        var clips=library.Search("Wait1",0,out _);Check(clips.Count==2&&clips.Select(c=>c.Clip.ResourceKey).Distinct().Count()==2,"Repeated clip and branch names retain distinct native index identities");
        foreach(var clip in clips)using(var lease=library.Open(clip.Clip.Id))Check(lease.Reference.MotionKey==clip.Clip.ResourceKey,"Duplicate-name source opens the exact saved native branch");
    }
    private static void SearchScale(AnimationLibrary source,string output)
    {
        string root=Path.Combine(output,"MetadataScale");Directory.CreateDirectory(root);
        var xml=XDocument.Load(source.IndexPath);var clips=xml.Root.Element("Clips");var example=clips.Elements().First();
        while(clips.Elements().Take(5000).Count()<5000){var copy=new XElement(example);copy.Element("Id").Value=Guid.NewGuid().ToString("N");clips.Add(copy);}
        xml.Save(Path.Combine(root,"catalogue.xml"));var clock=Stopwatch.StartNew();
        var result=new AnimationLibrary(root).Search("",0,out int total);
        Check(total==5000&&result.Count==100&&clock.ElapsedMilliseconds<5000,"Five thousand metadata rows search with bounded paging and no clip decoding",new{Rows=total,Page=result.Count,Milliseconds=clock.ElapsedMilliseconds});
    }
    private static void ManageSources(AnimationLibrary library,string output)
    {
        var baseline=library.Read();var removed=baseline.Models.First();string modelHash=Hash(baseline.Mounts.Single(m=>m.Id==removed.MountId).LocalPath);
        string project=Path.Combine(output,"Snake-B.fpstudio"),projectHash=Hash(project);
        library.RemoveSource(removed.Id);var after=library.Read();
        Check(!after.Models.Any(m=>m.Id==removed.Id)&&!after.Clips.Any(c=>c.ModelId==removed.Id),"Remove Source unregisters its model and complete motion-set entries");
        Check(Hash(baseline.Mounts.Single(m=>m.Id==removed.MountId).LocalPath)==modelHash&&Hash(project)==projectHash,"Source removal preserves original bytes and independent saved fighters");
        using(var saved=FighterProject.Open(project))Check(saved.Animations.Count==1,"Previously baked fighter reopens after its library source is removed");
        library.UndoLastUpdate();Check(library.Read().Clips.Count==baseline.Clips.Count&&library.Read().Models.Any(m=>m.Id==removed.Id),"Undo Source removal restores every original immutable ID");
        foreach(string game in AnimationLibrary.PlannedGames)library.SetGameLocation(game,"Local test build",output);
        Check(new AnimationLibrary(library.DirectoryPath).Read().GameLocations.Count==AnimationLibrary.PlannedGames.Length,"Every planned game's build path persists across library clients");
        Check(library.Read().Clips.Count==baseline.Clips.Count,"Saving future game paths never claims indexed clips or new native support");
        var location=library.Read().GameLocations.First();library.RemoveGameLocation(location.Id);Check(!library.Read().GameLocations.Any(l=>l.Id==location.Id),"Game/build path removal is independent of indexed source assets");
        library.UndoLastUpdate();Check(library.Read().GameLocations.Count==AnimationLibrary.PlannedGames.Length,"Game/build path changes are undoable");
        library.SetGameLocation("Additional Game","Build 1",output);library.SetGameLocation("Project M","SD build A",output);library.SetGameLocation("Project M","SD build B",output);
        var configured=new AnimationLibrary(library.DirectoryPath).Read().GameLocations;
        Check(configured.Any(l=>l.Game=="Additional Game")&&configured.Count(l=>l.Game=="Project M"&&l.GameVersion.StartsWith("SD build"))==2,"Custom games and multiple Project M SD builds retain independent paths");
        Refuses(()=>library.ProtectOutputPath(Path.Combine(output,"new-output.chr0")),"Configured game/install folders stay read-only for native output");
    }
}
