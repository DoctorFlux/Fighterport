using FighterPort.Core;

namespace FighterPort.Adapters.Brawl;

public sealed partial class BrawlAdapter:IAnimationFileWriter
{
    public AnimationFileFormat Format {get;}=new("brawl.chr0","Brawl skeletal animation","chr0");
    public bool Supports(FighterProject project,AnimationClip clip)=>project.SourceModels.Any(m=>m.Resource.Format=="MDL0");
    public async Task<AnimationFileWriteReceipt> WriteAnimationAsync(FighterProject project,AnimationClip clip,string path,CancellationToken cancellation=default)
    {
        path=AnimationFileWriting.NewDestination(project,path,Format.Extension);string staged=path+"."+Guid.NewGuid().ToString("N")+".tmp.chr0";
        try
        {
            var output=await ExportAsync(project,clip,staged,cancellation);
            if(output.Project is null)throw new InvalidDataException("The native writer did not return a freshly reopened animation.");
            cancellation.ThrowIfCancellationRequested();File.Move(staged,path,false);
            return new(Format.Id,path,ProjectStore.Hash(path),true,output.Diagnostics);
        }
        finally{if(File.Exists(staged))File.Delete(staged);}
    }
}
