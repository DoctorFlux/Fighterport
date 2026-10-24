namespace FighterPort.Core;

public sealed record AnimationFileFormat(string Id,string Name,string Extension);
public sealed record AnimationFileWriteReceipt(string Format,string Path,string Sha256,bool Reopened,string[] Diagnostics);

// Writers advertise only formats they can actually write. Future adapters add
// this contract; neither the document nor the save UI needs native Brawl types.
public interface IAnimationFileWriter
{
    AnimationFileFormat Format {get;}
    bool Supports(FighterProject project,AnimationClip clip);
    Task<AnimationFileWriteReceipt> WriteAnimationAsync(FighterProject project,AnimationClip clip,string path,CancellationToken cancellation=default);
}
public static class AnimationFileWriting
{
    public static string NewDestination(FighterProject project,string path,string extension)
    {
        path=System.IO.Path.GetFullPath(path);
        if(!string.Equals(System.IO.Path.GetExtension(path),"."+extension,StringComparison.OrdinalIgnoreCase))throw new NotSupportedException("Choose a ."+extension+" output for this writer.");
        if(ProjectStore.PreservedSourcePaths(project).Any(source=>string.Equals(System.IO.Path.GetFullPath(source),path,StringComparison.OrdinalIgnoreCase)))throw new IOException("Save to a new file; original source and target files are preserved.");
        if(File.Exists(path)||Directory.Exists(path))throw new IOException("The output already exists. Choose a new file name.");return path;
    }
}
public sealed class ProjectAnimationFileWriter:IAnimationFileWriter
{
    public AnimationFileFormat Format {get;}=new("fighterport.project","FighterPort animation project","fighterport");
    public bool Supports(FighterProject project,AnimationClip clip)=>project.Scene.Bones.Length>0;
    public async Task<AnimationFileWriteReceipt> WriteAnimationAsync(FighterProject project,AnimationClip clip,string path,CancellationToken cancellation=default)
    {
        path=AnimationFileWriting.NewDestination(project,path,Format.Extension);
        if(!project.Clips.Any(c=>c.Id==clip.Id&&ReferenceEquals(c,clip)))throw new InvalidOperationException("The selected animation does not belong to this project snapshot.");
        var changedSources=await Task.Run(()=>ProjectStore.ValidateSources(project),cancellation);if(changedSources.Length>0)throw new InvalidDataException(string.Join("\n",changedSources.Select(d=>d.Message)));
        string staged=path+"."+Guid.NewGuid().ToString("N")+".tmp.fighterport";
        try{await ProjectStore.SaveAsync(project,staged,cancellation);File.Move(staged,path,false);}finally{if(File.Exists(staged))File.Delete(staged);}
        var reopened=await ProjectStore.LoadAsync(path,cancellation);
        if(reopened.Scene.SkeletonRevision!=project.Scene.SkeletonRevision||reopened.Clips.All(c=>c.Id!=clip.Id||c.Revision!=clip.Revision))throw new InvalidDataException("Saved animation project did not reopen with its exact rig and animation revision.");
        return new(Format.Id,path,ProjectStore.Hash(path),true,["Saved project snapshot and reopened its exact rig/animation identity. Native game writing and motion/contact validation are separate."]);
    }
}
