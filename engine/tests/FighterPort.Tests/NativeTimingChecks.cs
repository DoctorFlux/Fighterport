using System.Numerics;
using FighterPort.Core;
using FighterPort.Adapters.Brawl;

internal static class NativeTimingChecks
{
    public static async Task RunAsync(BrawlAdapter adapter,FighterProject source,string directory,Action<bool,string> check)
    {
        var basis=source.Clips[0].Source!;var clip=new AnimationClip(Guid.NewGuid(),"Original nonloop boundary",24,new(60),false,[new(source.Scene.Bones[0].Name,[null,null,null,null,null,null,new([new(0,0,1,1),new(24,24,1,1)]),null,null])]){NativeTarget=basis,NativeWrite=NativeWriteMode.NewResource,NativeWriteName="OriginalNonloopBoundary"};
        string path=Path.Combine(directory,"nonloop-boundary.chr0");await adapter.ExportAsync(source with{Clips=[clip]},clip,path);double[] times=[23,23.125,23.5,23.875];var reopened=await adapter.RunAsync(new("import",basis.Model!.Path,path,null,null,[],times));
        check(reopened.Project!.Clips[0].SampleCount==24&&!reopened.Project.Clips[0].Loop&&reopened.Project.Clips[0].LastKeyFrame==24,"Native non-loop motion retains 24 displayed samples and a separate end-exclusive boundary key at 24");
        check(times.Select((f,i)=>Math.Abs(reopened.ExpectedBonePositions![i][0][0]-(float)f)).Max()<.00001f,"Independent BrawlLib samples the final non-loop fractional segment toward its boundary key without an extra displayed endpoint");
    }
}
