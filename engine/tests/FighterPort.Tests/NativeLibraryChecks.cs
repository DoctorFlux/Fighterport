using FighterPort.Core;
using FighterPort.Adapters.Brawl;

internal static class NativeLibraryChecks
{
    public static async Task RunAsync(BrawlAdapter adapter,string libraryRoot,string targetPath,string output,Action<bool,string> check,Action<float,float,float,string> near)
    {
        var library=new AnimationLibraryStore(libraryRoot);var source=(await library.LoadAsync()).Sources.Single();
        var selected=source.Clips.Single(c=>c.Name.StartsWith("Wait1 [",StringComparison.Ordinal));var donor=await library.OpenClipAsync(source.Id,selected.Id);var target=await ProjectStore.LoadAsync(targetPath);
        var originals=source.Originals.Select(b=>(b.OriginalPath,b.Sha256)).ToArray();string modelHash=AnimationControls.DeformChecksum(donor.Scene),cacheHash=ProjectStore.Hash(Path.Combine(libraryRoot,"prepared",source.PackageHash+".fighterport"));
        var identity=donor.Clips[0].Source??throw new InvalidDataException("Missing native source identity.");
        check(identity.Resource.OriginalPath is not null&&identity.Model!.OriginalPath is not null&&ProjectStore.ValidateSources(donor).Length==0,"Native library uses verified cached model/motion bytes and preserves authentic source identities");
        check(source.Unresolved.Length>0&&source.Unresolved.All(c=>c.MissingBones.Length>0)&&!source.Description.BuildVerified,"Native catalogue reports exact foreign-rig channels and keeps the supplied game build unverified");
        var rig=BrawlControlProfiles.SetUp(donor);var targetRig=BrawlControlProfiles.SetUp(target);var result=RetargetService.Bake(donor,donor.Clips[0],rig,target,targetRig,ground:new());check(result.Value is not null,"Reviewed source-library anatomy transfers with automatic support to the other existing native rig: "+string.Join("; ",result.Diagnostics.Where(d=>d.Error).Select(d=>d.Message)));
        var applied=RetargetService.Apply(target,result.Value!);var doc=new EditorDocument(target);doc.Transaction("Use library",p=>RetargetService.Apply(p,result.Value!));doc.Undo();check(doc.Project.Clips.Length==target.Clips.Length,"Native library transfer undoes as one complete target transaction");doc.Redo();
        string save=Path.Combine(output,"library-target.fighterport");await ProjectStore.SaveAsync(applied,save);var saved=await ProjectStore.LoadAsync(save);var provenance=saved.LibrarySources.Single(s=>s.Id==result.Value!.Source.Id);
        check(provenance.LibraryRegistrationId==source.Id&&provenance.Build==source.Description.Build&&provenance.SourceFiles.Length==source.Originals.Length&&provenance.Clip.Source!.Resource.Sha256==selected.Native!.Sha256,"Native target save/reopen retains library/game/build/hash and original source-model provenance");
        // Existing independent native matrix/vertex, rate conversion and loop boundary oracles.
        await NativeRetargetChecks.RunAsync(adapter,donor,target,output,check,near,ground:true);
        check(modelHash==AnimationControls.DeformChecksum((await library.OpenClipAsync(source.Id,selected.Id)).Scene)&&originals.All(o=>ProjectStore.Hash(o.OriginalPath)==o.Sha256)&&cacheHash==ProjectStore.Hash(Path.Combine(libraryRoot,"prepared",source.PackageHash+".fighterport")),"Library/native export leaves original game inputs, cached package and source deform rig unchanged");
    }
}
