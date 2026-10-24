using System.Text.Json;
using FighterPort.Adapters.Brawl;
using FighterPort.Core;

internal static class MultiModelLibraryChecks
{
    public static async Task Run(string marioPath,string snakePath,string directory,Action<bool,string> check)
    {
        var mario=await ProjectStore.LoadAsync(marioPath);var snake=await ProjectStore.LoadAsync(snakePath);
        string marioRig=AnimationControls.DeformChecksum(mario.Scene),snakeRig=AnimationControls.DeformChecksum(snake.Scene);
        var store=new AnimationLibraryStore(Path.Combine(directory,"library"));
        LibrarySourceDescription Description(string character)=>new(BrawlAdapter.ReaderVersion,new("Brawl","Wii","Supplied version unverified"),character,"Original model","Supplied authentic Mario/Snake model and separate motions",true);
        LibraryInput[] Inputs(FighterProject project)=>[new("model",project.SourceModels[0].Resource.Path),new("motion",project.Clips[0].Source!.Resource.Path)];
        var first=await store.RegisterAsync(mario,Description("Mario"),Inputs(mario));
        // Reproduce the historical catalogue identity while retaining genuine
        // raw/prepared proofs. A second rig must not collide with that entry.
        first=first with{Identity=Convert.ToHexString(System.Security.Cryptography.SHA256.HashData("{}"u8))};
        await File.WriteAllTextAsync(Path.Combine(store.Root,"catalogue.json"),JsonSerializer.Serialize(new LibraryCatalogue(1,[first]),ProjectStore.Json));
        var second=await store.RegisterAsync(snake,Description("Snake"),Inputs(snake));
        check(first.Id!=second.Id&&(await store.LoadAsync()).Sources.Length==2,"Registering Snake after Mario retains both authentic model-bound catalogues");
        var firstAgain=await store.OpenSourceAsync(first.Id);var secondAgain=await store.OpenSourceAsync(second.Id);
        check(firstAgain.Scene.Bones.Length==63&&secondAgain.Scene.Bones.Length==70,"Library models reopen with their distinct actual Mario/Snake skeletons");
        check(firstAgain.Clips.Any(c=>c.Source?.NativeName=="Wait1")&&secondAgain.Clips.Any(c=>c.Source?.NativeName=="Wait1")&&firstAgain.Clips.Any(c=>c.Source?.NativeName=="DownWaitU")&&secondAgain.Clips.Any(c=>c.Source?.NativeName=="CliffClimbSlow"),"Same-name motions remain independently associated with each original model");
        bool wrong=false;try{await store.OpenClipAsync(first.Id,second.Clips[0].Id);}catch(InvalidDataException){wrong=true;}check(wrong,"A Snake clip cannot be opened against the Mario registration");
        var duplicate=await store.RegisterAsync(mario,Description("Mario"),Inputs(mario));check(duplicate.Id==first.Id&&(await store.LoadAsync()).Sources.Length==2,"Re-registering Mario does not replace Snake or duplicate Mario");
        var modelOnly=await store.RegisterAsync(snake with{Clips=[],Contacts=[]},Description("Snake model-only"),[Inputs(snake)[0]]);
        var restarted=new AnimationLibraryStore(store.Root);var bare=await restarted.OpenSourceAsync(modelOnly.Id);
        check((await restarted.LoadAsync()).Sources.Length==3&&modelOnly.Clips.Length==0&&bare.Clips.Length==0&&bare.Scene.Bones.Length==70,"A model-only library entry survives restart without inventing an animation");
        var character=CharacterWorkspace.Empty("Mario and Snake");Guid marioScene=character.AddScene(firstAgain),snakeScene=character.AddScene(secondAgain);
        character.Activate(null,marioScene);character.Document!.SelectClip(Array.FindIndex(firstAgain.Clips,c=>c.Source?.NativeName=="Wait1"));character.Document.Seek(2.25);var before=character.Document.AuthoringProject;
        character.Activate(null,snakeScene);character.Document!.CreateAnimation("Independent Snake authoring",24,new(60),true);
        character.Activate(null,marioScene);check(ReferenceEquals(character.Document!.AuthoringProject,before)&&character.Document.Clock.Frame==2.25,"Adding and switching models preserves Mario work when Snake gains another editable motion");
        string project=Path.Combine(directory,"Character.fighterport");await character.SaveAsync(project);var reopened=await CharacterStore.LoadAsync(project);reopened.Activate(null,snakeScene);
        check(reopened.Project.Scenes.Length==2&&reopened.Document!.Project.Clips.Any(c=>c.Name=="Independent Snake authoring"),"Both managed model sessions and Snake authoring reopen in one character project");
        check(AnimationControls.DeformChecksum(firstAgain.Scene)==marioRig&&AnimationControls.DeformChecksum(secondAgain.Scene)==snakeRig&&AnimationControls.DeformChecksum(bare.Scene)==snakeRig,"Library registration/reuse preserves both native skeletons, binds and weights");
        foreach(var s in new[]{first,second,modelOnly})await restarted.VerifySourceAsync(s);
        check(new[]{first,second,modelOnly}.All(s=>s.Originals.All(b=>ProjectStore.Hash(b.OriginalPath)==b.Sha256)),"All original source hashes remain unchanged after sequential multi-model registration and project save");

        string buildRoot=Path.Combine(directory,"ExtractedBuild");string fighter=Path.Combine(buildRoot,"pf","fighter");
        foreach(var pair in new[]{("mario",Inputs(mario)),("snake",Inputs(snake))})foreach(var input in pair.Item2){string dir=Path.Combine(fighter,pair.Item1);Directory.CreateDirectory(dir);File.Copy(input.Path,Path.Combine(dir,Path.GetFileName(input.Path)),false);}
        var mount=SourceRegistry.Create("Project+ Main","Project+","Wii","Unverified supplied assets",SourceKind.ModBuild,buildRoot);
        var practice=SourceRegistry.Create("Project+ Practice","Project+","Wii","Unverified supplied assets",SourceKind.ModBuild,buildRoot);
        string registrations=Path.Combine(directory,"sources.json");await SourceRegistry.SaveAsync([mount,practice],registrations);var saved=await SourceRegistry.LoadAsync(registrations);
        check(saved.Length==2&&saved[0].Id!=saved[1].Id,"Multiple builds of the same game retain independent registration identities");
        var inventory=await BrawlGameSources.BrowseAsync(mount);check(inventory.Files.Length==4&&inventory.Files.Select(p=>p.Split('/')[1]).Distinct().Count()==2,"Extracted build discovery reads both real character directories without decoding during browsing");
        var direct=await BrawlGameSources.BrowseAsync(mount with{Path=fighter+Path.DirectorySeparatorChar});check(direct.Files.SequenceEqual(inventory.Files),"An explicitly selected fighter folder with a trailing separator resolves the same exact build");
        var selected=inventory.Files.Where(p=>p.StartsWith("fighter/snake/",StringComparison.Ordinal)).ToArray();var prepared=await BrawlGameSources.PrepareAsync(inventory,selected,Path.Combine(directory,"prepared"));
        check(prepared.Files.Count==2&&selected.All(p=>ProjectStore.Hash(prepared.Files[p])==ProjectStore.Hash(Path.Combine(fighter,p[8..].Replace('/',Path.DirectorySeparatorChar))))&&File.Exists(prepared.Receipt),"Selected native packages are preserved exactly with an independent intake receipt");
        bool traversal=false;try{await BrawlGameSources.PrepareAsync(inventory,["fighter/../outside.pac"],Path.Combine(directory,"rejected"));}catch(InvalidDataException){traversal=true;}check(traversal&&!Directory.Exists(Path.Combine(directory,"rejected")),"Invalid game resource identities are rejected before creating outputs");
        using var cancelled=new CancellationTokenSource();cancelled.Cancel();bool stopped=false;try{await BrawlGameSources.PrepareAsync(inventory,selected,Path.Combine(directory,"cancelled"),cancelled.Token);}catch(OperationCanceledException){stopped=true;}check(stopped,"Cancelled source preparation does not publish a successful receipt");
        await File.WriteAllTextAsync(Path.Combine(directory,"model-library-receipt.json"),JsonSerializer.Serialize(new{Models=new[]{new{first.Id,first.SkeletonFingerprint,Count=first.Clips.Length},new{second.Id,second.SkeletonFingerprint,Count=second.Clips.Length}},ModelOnly=modelOnly.Id,CharacterScenes=reopened.Project.Scenes.Length,NativeInputsUnchanged=true,Claim="Actual supplied model/library and extracted-folder checks. Disc/native-curve/game checks remain separate."},new JsonSerializerOptions(ProjectStore.Json){IgnoreReadOnlyProperties=false}));
    }
}
