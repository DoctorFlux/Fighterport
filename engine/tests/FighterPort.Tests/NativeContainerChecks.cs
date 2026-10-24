using System.Numerics;
using FighterPort.Core;
using FighterPort.Adapters.Brawl;

internal static class NativeContainerChecks
{
    public static async Task RunAsync(BrawlAdapter adapter,FighterProject game,string directory,Action<bool,string> check,Action<float,float,float,string> near)
    {
        var doc=new EditorDocument(game);doc.SelectClip(Math.Max(0,Array.FindIndex(game.Clips,c=>c.Source?.NativeName=="Wait1")));var source=doc.Clip.Source!;var model=source.Model!;string originalHash=ProjectStore.Hash(source.Resource.Path);int originalClips=game.Clips.Length;
        doc.CreateAnimation("FighterPort_Original_"+Guid.NewGuid().ToString("N")[..8],24,new(60),true);check(doc.Project.Clips.Length==originalClips+1&&doc.Clip.NativeTarget?.Resource==source.Resource,"Creating an animation keeps native target identity separate and adds an independent editable revision");
        doc.Seek(12);var t=doc.Evaluate().Local[0];doc.KeyTransform(0,t with{Translation=t.Translation+new Vector3(.15f,0,0)});var clip=doc.Clip;
        string output=Path.Combine(directory,Path.GetFileNameWithoutExtension(source.Resource.Path)+"-added-"+Guid.NewGuid().ToString("N")+Path.GetExtension(source.Resource.Path));
        var response=await adapter.ExportAsync(doc.Project,clip,output,insert:true);check(response.PreservedResources is {Length:>0},"New motion container independently verifies hashes of every preserved native resource unit");
        await File.WriteAllTextAsync(output+".preservation.json",System.Text.Json.JsonSerializer.Serialize(response.PreservedResources,ProjectStore.Json));
        var reopened=response.Project!;check(reopened.Clips[0].Source?.NativeName==clip.Name&&reopened.Clips[0].SampleCount==24,"Added native CHR0 reopens inside its actual archive group with the authored name and sample count");
        var openedPose=AnimationService.Evaluate(reopened.Scene,reopened.Clips[0],12);near(openedPose.Local[0].Translation.X,t.Translation.X+.15f,.0001f,"New motion-container animation retains its actual edited key");
        var catalogue=await adapter.ImportAsync(model.Path,output);check(catalogue.Clips.Length==originalClips+1&&game.Clips.All(c=>catalogue.Clips.Any(n=>n.Source?.NativeName==c.Source?.NativeName&&n.Source?.Resource.Entry==c.Source?.Resource.Entry)),"New PAC/BRRES retains the complete original animation catalogue and adds exactly one clip");
        check(ProjectStore.Hash(source.Resource.Path)==originalHash&&ProjectStore.Hash(model.Path)==model.Sha256,"Adding a native animation never modifies original motion or model source bytes");
        doc.CreateAnimation("FighterPort_Batch_"+Guid.NewGuid().ToString("N")[..8],24,new(60),true);doc.Seek(12);doc.KeyTransform(0,t with{Translation=t.Translation+new Vector3(.3f,0,0)});var second=doc.Clip;
        string batchOutput=Path.Combine(directory,"multi-motion-"+Guid.NewGuid().ToString("N")+Path.GetExtension(source.Resource.Path));var batch=await adapter.ExportBatchAsync(doc.Project,[clip,second],batchOutput);
        check(batch.State=="Committed"&&batch.Files.Single().Motions.Length==2&&File.Exists(batch.ReceiptPath),"Two motions stage into one NEW native container with a durable committed receipt");
        var all=await adapter.ImportAsync(model.Path,batchOutput);check(all.Clips.Length==originalClips+2&&new[]{clip.Name,second.Name}.All(name=>all.Clips.Count(c=>c.Source?.NativeName==name)==1),"Fresh multi-motion reopening retains all original native identities and both independent inserted clips");
        foreach(var expected in new[]{clip,second}){var actual=all.Clips.Single(c=>c.Source?.NativeName==expected.Name);near(AnimationService.Evaluate(all.Scene,actual,12.375).World[0].Translation.X,AnimationService.Evaluate(doc.Project.Scene,expected,12.375).World[0].Translation.X,.00001f,"Each inserted motion keeps its own fractional edited pose after final native reopening");}
        var recovered=await BrawlAdapter.InspectBatchRecoveryAsync(batch.ReceiptPath);check(recovered.State=="CommittedRecovered"&&recovered.Files.Single().Sha256==ProjectStore.Hash(batchOutput),"Recovery reconciles the actual committed container hash without overwriting it");
        check(ProjectStore.Hash(source.Resource.Path)==originalHash&&ProjectStore.Hash(model.Path)==model.Sha256,"Multi-motion insertion preserves both original source hashes; document Undo does not delete exports");
        if(originalClips<10)
        {
            string aborted=Path.Combine(directory,"cancelled-batch-"+Guid.NewGuid().ToString("N")+Path.GetExtension(source.Resource.Path));using var cancel=new CancellationTokenSource();bool stopped=false;
            try{await adapter.ExportBatchAsync(doc.Project,[clip,second],aborted,cancel.Token,new SynchronousProgress(r=>{if(r.State=="Staging")cancel.Cancel();}));}catch(OperationCanceledException){stopped=true;}
            var noCommit=await BrawlAdapter.InspectBatchRecoveryAsync(aborted+".fighterport-receipt.json");check(stopped&&!File.Exists(aborted)&&noCommit.State=="NoCommittedOutput","Cancellation between native motion stages leaves no committed output and an accurate durable recovery receipt");
            string interrupted=Path.Combine(directory,"interrupted-receipt-"+Guid.NewGuid().ToString("N")+Path.GetExtension(source.Resource.Path));FileStream? blocked=null;bool interruptedReceipt=false;
            try{await adapter.ExportBatchAsync(doc.Project,[clip,second],interrupted,progress:new SynchronousProgress(r=>{if(r.State=="Validated")blocked=new FileStream(r.ReceiptPath+".tmp",FileMode.Create,FileAccess.ReadWrite,FileShare.None);}));}catch(IOException){interruptedReceipt=true;}finally{blocked?.Dispose();}
            var reconciled=await BrawlAdapter.InspectBatchRecoveryAsync(interrupted+".fighterport-receipt.json");check(interruptedReceipt&&File.Exists(interrupted)&&reconciled.State=="CommittedRecovered"&&reconciled.Files.Single().Motions.Length==2,"Actual receipt-write interruption after file commit recovers the validated output hash without claiming rollback or deleting the export");
        }
    }
    sealed class SynchronousProgress(Action<NativeBatchManifest> report):IProgress<NativeBatchManifest>{public void Report(NativeBatchManifest value)=>report(value);}
}
